using ERP.Inventory.Application.Common;
using ERP.Inventory.Application.DTOs;
using ERP.Inventory.Application.Interfaces;
using ERP.Inventory.Domain.Entities;
using ERP.Inventory.Domain.Enums;
using ERP.Inventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ERP.Inventory.Infrastructure.Services;

/// <summary>
/// Kiểm kê kho theo phiên (session-based):
///   1. CreateSessionAsync   — Tạo phiếu CHK với SessionStatus = InProgress.
///   2. ScanBatchAsync        — Ghi các lines scan (Matched / WrongLocation / Extra). Có thể gọi nhiều lần.
///   3. FinalizeAsync         — Tính Missing (items InStock chưa được scan), cập nhật status. Chỉ gọi 1 lần khi đã scan xong.
/// </summary>
public sealed class InventoryCheckService : InventoryOperationBase
{
    public InventoryCheckService(InventoryDbContext db, IDocumentNumberService documentNumbers, IDateTimeProvider clock)
        : base(db, documentNumbers, clock) { }

    // ─── 1. Create Session ────────────────────────────────────────────────────

    /// <summary>
    /// Tạo phiếu kiểm kê mới ở trạng thái InProgress.
    /// Không cần lines ngay — người dùng sẽ gửi scan batches sau.
    /// </summary>
    public async Task<ServiceResult<PostedDocumentDto>> CreateSessionAsync(InventoryCheckSessionRequest request, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        var warehouse = await FindWarehouseByIDAsync(request.WarehouseId, cancellationToken);
        if (warehouse == null) return ServiceResult<PostedDocumentDto>.Fail($"Warehouse not found.");
        if (!user.CanAccessWarehouse(request.WarehouseId)) return ServiceResult<PostedDocumentDto>.Fail("Permission denied for inventory check warehouse.");
        if (string.IsNullOrWhiteSpace(request.CountMethod)) return ServiceResult<PostedDocumentDto>.Fail("Count method is required.");
        if (string.IsNullOrWhiteSpace(request.ResponsibleStaff)) return ServiceResult<PostedDocumentDto>.Fail("Responsible staff is required.");

        var now = _clock.UtcNow;
        string documentNo = await NextCheckNoAsync(request.SessionDate, request.DocumentPeriodType, cancellationToken);
        var document = await _db.InventoryCheckDocuments.FirstOrDefaultAsync(x => x.DocumentNo == documentNo, cancellationToken);
        if(document == null)
        {
            document = new InventoryCheckDocument
            {
                DocumentNo = documentNo,
                DocumentDate = request.SessionDate,
                WarehouseId = warehouse.Id,
                CountMethod = request.CountMethod,
                ResponsibleStaff = request.ResponsibleStaff,
                SessionStatus = "InProgress",
                // DocumentBase.Status tetap Posted — SessionStatus track tiến trình riêng
                CreatedAt = request.SessionDate,
                CreatedBy = user.UserName,
                ApprovedBy = user.UserName,
                ApprovedAt = request.SessionDate,
                PostedAt = request.SessionDate,
                Note = request.Note
            };
            _db.InventoryCheckDocuments.Add(document);
            await _db.SaveChangesAsync(cancellationToken);
        }
        
        return ServiceResult<PostedDocumentDto>.Ok(ToPostedDto(nameof(InventoryCheckDocument), document.Id, document.DocumentNo, now),$"Inventory check session created: {document.DocumentNo}");
    }

    // ─── 2. Scan Batch ────────────────────────────────────────────────────────

    /// <summary>
    /// Gửi một batch scan vào phiên kiểm kê đang InProgress.
    /// Chỉ xử lý Matched / WrongLocation / Extra — KHÔNG tính Missing.
    /// Có thể gọi nhiều lần cho đến khi Finalize.
    /// </summary>
    public async Task<ServiceResult<ScanBatchResultDto>> ScanBatchAsync(InventoryCheckScanRequest request, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        var document = await _db.InventoryCheckDocuments
            .Include(x => x.Warehouse)
            .FirstOrDefaultAsync(x => x.Id == request.DocumentId, cancellationToken);
        if (document == null) return ServiceResult<ScanBatchResultDto>.Fail("Inventory check document not found.");
        if (document.SessionStatus == "Finalized") return ServiceResult<ScanBatchResultDto>.Fail("This inventory check session has already been finalized.");
        if (!user.CanAccessWarehouse(document.WarehouseId)) return ServiceResult<ScanBatchResultDto>.Fail("Permission denied.");
        if (!request.Lines.Any()) return ServiceResult<ScanBatchResultDto>.Fail("At least one scan line is required.");

        var warehouse = document.Warehouse!;

        // Validate lines trước
        foreach (var line in request.Lines)
        {
            if (string.IsNullOrWhiteSpace(line.ItemCode)) return ServiceResult<ScanBatchResultDto>.Fail("ItemCode is required for every check line.");
            if (string.IsNullOrWhiteSpace(line.SerialNumber)) return ServiceResult<ScanBatchResultDto>.Fail("SerialNumber is required for every check line.");
            if (string.IsNullOrWhiteSpace(line.BinCode)) return ServiceResult<ScanBatchResultDto>.Fail("BinCode is required for every check line.");
            var bin = await FindBinByCodeAsync(line.BinCode, cancellationToken);
            if (bin == null) return ServiceResult<ScanBatchResultDto>.Fail($"BinCode {line.BinCode} not found.");
            if (bin.WarehouseId != document.WarehouseId) return ServiceResult<ScanBatchResultDto>.Fail($"BinCode {line.BinCode} does not belong to this warehouse.");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var now = _clock.UtcNow;

        // Lấy danh sách SerialNumber đã scan trong phiên này để tránh duplicate
        var alreadyScannedInstanceIds = (await _db.InventoryCheckLines
            .Where(x => x.InventoryCheckDocumentId == document.Id && x.Result != InventoryCheckLineResult.Missing&& x.ItemInstanceId.HasValue)
            .Select(x => x.ItemInstanceId!.Value)
            .ToListAsync(cancellationToken)).ToHashSet();

        int matched = 0, wrongLocation = 0, extra = 0, skipped = 0;

        foreach (var line in request.Lines)
        {
            var actualBin = (await FindBinByCodeAsync(line.BinCode, cancellationToken))!;
            var instance = await FindInstanceByCodeAsync(line.ItemCode, line.SerialNumber, cancellationToken);

            if(await _db.InventoryCheckLines.FirstOrDefaultAsync(x => x.InventoryCheckDocumentId == document.Id && x.ActualBinLocationId == actualBin.Id, cancellationToken) != null)
            {
                return ServiceResult<ScanBatchResultDto>.Fail($"BinCode is duplicated in this document.");
            }

            if (instance == null)
            {
                // === EXTRA: item không có trong DB → tạo mới ===
                var item = await FindItemByCodeAsync(line.ItemCode, cancellationToken);
                if (item == null)
                {
                    // Bỏ qua dòng này, ghi warning vào note
                    _db.InventoryCheckLines.Add(new InventoryCheckLine
                    {
                        InventoryCheckDocumentId = document.Id,
                        SystemBinLocationId = null, ActualBinLocationId = actualBin.Id,
                        Result = InventoryCheckLineResult.Extra,
                        Note = $"[UNKNOWN ITEM TYPE] {line.ItemCode} / {line.SerialNumber}",
                        CreatedAt = now, CreatedBy = user.UserName
                    });
                    extra++;
                    continue;
                }

                var newInstance = new ItemInstance
                {
                    ItemId = item.Id,
                    SerialNumber = line.SerialNumber.Trim(),
                    Barcode = line.SerialNumber.Trim(),
                    Status = ItemStatus.Normal,
                    CreatedAt = now, CreatedBy = user.UserName
                };
                _db.ItemInstances.Add(newInstance);
                await _db.SaveChangesAsync(cancellationToken);

                _db.CurrentItemLocations.Add(new CurrentItemLocation
                {
                    ItemInstanceId = newInstance.Id, LocationType = LocationType.BinLocation,
                    WarehouseId = warehouse.Id, BinLocationId = actualBin.Id,
                    ReferenceDocumentType = nameof(InventoryCheckDocument), ReferenceDocumentId = document.Id,
                    ReferenceDocumentNo = document.DocumentNo,
                    UpdatedLocationAt = now, UpdatedLocationBy = user.UserName,
                    CreatedAt = now, CreatedBy = user.UserName
                });
                await ApplyStockDeltaAsync(warehouse.Id, actualBin.Id, item.Id, ItemStatus.Normal, 1, user, cancellationToken);

                _db.InventoryCheckLines.Add(new InventoryCheckLine
                {
                    InventoryCheckDocumentId = document.Id, ItemInstanceId = newInstance.Id,
                    SystemBinLocationId = null, ActualBinLocationId = actualBin.Id,
                    Result = InventoryCheckLineResult.Extra,
                    Note = line.Note ?? $"Extra item found at {actualBin.BinCode}.",
                    CreatedAt = now, CreatedBy = user.UserName
                });
                extra++;
            }
            else
            {
                // Bỏ qua nếu đã scan trong phiên này rồi
                if (alreadyScannedInstanceIds.Contains(instance.Id))
                {
                    skipped++;
                    continue;
                }
                alreadyScannedInstanceIds.Add(instance.Id);

                var current = await _db.CurrentItemLocations.FirstOrDefaultAsync(x => x.ItemInstanceId == instance.Id, cancellationToken);
                var systemBinId = current?.BinLocationId;

                if (systemBinId.HasValue && systemBinId.Value == actualBin.Id)
                {
                    // === MATCHED ===
                    _db.InventoryCheckLines.Add(new InventoryCheckLine
                    {
                        InventoryCheckDocumentId = document.Id, ItemInstanceId = instance.Id,
                        SystemBinLocationId = systemBinId, ActualBinLocationId = actualBin.Id,
                        Result = InventoryCheckLineResult.Matched,
                        Note = line.Note,
                        CreatedAt = now, CreatedBy = user.UserName
                    });
                    // Đảm bảo status = InStock nếu đang ở trạng thái lệch
                    if (instance.Status != ItemStatus.InStock && instance.Status != ItemStatus.Normal)
                        instance.Status = ItemStatus.InStock;
                    matched++;
                }
                else
                {
                    // === WRONG LOCATION: cập nhật vị trí ngay ===
                    var oldBinId = systemBinId;
                    _db.InventoryCheckLines.Add(new InventoryCheckLine
                    {
                        InventoryCheckDocumentId = document.Id, ItemInstanceId = instance.Id,
                        SystemBinLocationId = systemBinId, ActualBinLocationId = actualBin.Id,
                        Result = InventoryCheckLineResult.WrongLocation,
                        Note = line.Note ?? $"Found at {actualBin.BinCode} instead of expected location.",
                        CreatedAt = now, CreatedBy = user.UserName
                    });

                    if (current != null)
                    {
                        var fromWarehouseId = current.WarehouseId;
                        if (oldBinId.HasValue && fromWarehouseId.HasValue)
                            await ApplyStockDeltaAsync(fromWarehouseId.Value, oldBinId, instance.ItemId, instance.Status, -1, user, cancellationToken);

                        current.LocationType = LocationType.BinLocation;
                        current.WarehouseId = warehouse.Id;
                        current.BinLocationId = actualBin.Id;
                        current.ReferenceDocumentType = nameof(InventoryCheckDocument);
                        current.ReferenceDocumentId = document.Id;
                        current.ReferenceDocumentNo = document.DocumentNo;
                        current.UpdatedLocationAt = now;
                        current.UpdatedLocationBy = user.UserName;

                        var bin = await FindBinByIdAsync(oldBinId, cancellationToken);

                        await ApplyStockDeltaAsync(warehouse.Id, actualBin.Id, instance.ItemId, instance.Status, 1, user, cancellationToken);
                        AddHistory(instance.Id, MovementActionType.MoveLocation, LocationType.BinLocation, oldBinId, $"Bin {bin?.FullPath}", LocationType.BinLocation, actualBin.Id, actualBin.FullPath, instance.Status, instance.Status, nameof(InventoryCheckDocument), document.Id, document.DocumentNo, "Inventory check: wrong location corrected", user);
                    }
                    wrongLocation++;
                }
            }
        }

        // Update SessionStatus vẫn là InProgress
        document.UpdatedAt = now;
        document.UpdatedBy = user.UserName;

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<ScanBatchResultDto>.Ok(new ScanBatchResultDto
        {
            DocumentId = document.Id,
            DocumentNo = document.DocumentNo,
            BatchMatched = matched,
            BatchWrongLocation = wrongLocation,
            BatchExtra = extra,
            BatchSkipped = skipped,
            SessionStatus = document.SessionStatus
        }, $"Batch scanned: {matched} matched, {wrongLocation} wrong location, {extra} extra, {skipped} skipped.");
    }

    // ─── 3. Finalize ─────────────────────────────────────────────────────────

    /// <summary>
    /// Finalize phiên kiểm kê: tính Missing (items InStock trong kho chưa được scan),
    /// cập nhật status → Lost, update SessionStatus = Finalized.
    /// Chỉ gọi khi đã scan xong toàn bộ phần kho cần kiểm.
    /// </summary>
    public async Task<ServiceResult<PostedDocumentDto>> FinalizeAsync(int documentId, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        var document = await _db.InventoryCheckDocuments
            .Include(x => x.Warehouse)
            .FirstOrDefaultAsync(x => x.Id == documentId, cancellationToken);
        if (document == null) return ServiceResult<PostedDocumentDto>.Fail("Inventory check document not found.");
        if (document.SessionStatus == "Finalized") return ServiceResult<PostedDocumentDto>.Fail("This inventory check session has already been finalized.");
        if (!user.CanAccessWarehouse(document.WarehouseId)) return ServiceResult<PostedDocumentDto>.Fail("Permission denied.");

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var now = _clock.UtcNow;

        // Lấy tất cả ItemInstance đã được scan trong phiên này (Matched + WrongLocation + Extra)
        var scannedInstanceIds = (await _db.InventoryCheckLines
            .Where(x => x.InventoryCheckDocumentId == documentId && x.Result != InventoryCheckLineResult.Missing && x.ItemInstanceId.HasValue)
            .Select(x => x.ItemInstanceId!.Value).ToListAsync(cancellationToken)).ToHashSet();

        // === MISSING: items InStock trong kho chưa được scan ===
        var allInStockLocations = await _db.CurrentItemLocations
            .Include(x => x.ItemInstance)
            .Where(x => x.WarehouseId == document.WarehouseId && x.ItemInstance != null && x.ItemInstance.IsActive
                && (x.ItemInstance.Status == ItemStatus.InStock || x.ItemInstance.Status == ItemStatus.Normal || x.ItemInstance.Status == ItemStatus.Damaged || x.ItemInstance.Status == ItemStatus.Scrapped)
                && !scannedInstanceIds.Contains(x.ItemInstanceId)).ToListAsync(cancellationToken);

        foreach (var missingLoc in allInStockLocations)
        {
            var missingInstance = missingLoc.ItemInstance!;

            _db.InventoryCheckLines.Add(new InventoryCheckLine
            {
                InventoryCheckDocumentId = documentId,
                ItemInstanceId = missingInstance.Id,
                SystemBinLocationId = missingLoc.BinLocationId,
                ActualBinLocationId = null,
                Result = InventoryCheckLineResult.Missing,
                Note = "Item not found during inventory check — marked Lost.",
                CreatedAt = now, CreatedBy = user.UserName
            });

            var oldBinId = missingLoc.BinLocationId;
            var oldWarehouseId = missingLoc.WarehouseId;
            if (oldBinId.HasValue && oldWarehouseId.HasValue)
                await ApplyStockDeltaAsync(oldWarehouseId.Value, oldBinId, missingInstance.ItemId, missingInstance.Status, -1, user, cancellationToken);

            missingLoc.BinLocationId = null;
            missingLoc.ReferenceDocumentType = nameof(InventoryCheckDocument);
            missingLoc.ReferenceDocumentId = documentId;
            missingLoc.ReferenceDocumentNo = document.DocumentNo;
            missingLoc.UpdatedLocationAt = now;
            missingLoc.UpdatedLocationBy = user.UserName;

            if(missingInstance.Status  == ItemStatus.Normal || missingInstance.Status == ItemStatus.InStock) missingInstance.Status = ItemStatus.Lost;
            var bin = await FindBinByIdAsync(oldBinId, cancellationToken);

            AddHistory(missingInstance.Id, MovementActionType.InventoryCheck, LocationType.BinLocation, oldBinId, $"Bin {bin?.FullPath}", null, null, "Unknown", ItemStatus.InStock, ItemStatus.Lost, nameof(InventoryCheckDocument), documentId, document.DocumentNo, "Missing: not found during inventory check", user);
        }

        // Finalize document
        document.SessionStatus = "Finalized";
        document.UpdatedAt = now;
        document.UpdatedBy = user.UserName;

        AddPostSideEffects("InventoryCheckFinalize", nameof(InventoryCheckDocument), documentId, document.DocumentNo, user, "Inventory check posted.");
        AddFinalizeNotification(document, scannedInstanceIds.Count, allInStockLocations.Count, user);

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<PostedDocumentDto>.Ok(ToPostedDto(nameof(InventoryCheckDocument), documentId, document.DocumentNo, now),
            $"Inventory check finalized: {allInStockLocations.Count} missing items detected.");
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private void AddFinalizeNotification(InventoryCheckDocument document, int scannedCount, int missingCount, CurrentUserContext user)
    {
        if (string.IsNullOrWhiteSpace(user.UserId)) return;
        var now = _clock.UtcNow;
        var hasIssues = missingCount > 0;

        _db.Notifications.Add(new Notification
        {
            UserId = user.UserId,
            Title = NotifyText(hasIssues ? "Inventory check action required" : "Inventory check result"),
            Message_Vi = hasIssues
                ? $"Kiểm kê phát hiện {missingCount} hàng thiếu, {scannedCount} hàng đã quét. {document.DocumentNo}"
                : $"Kiểm kê hoàn tất, không phát hiện chênh lệch. {document.DocumentNo}",
            Message_En = hasIssues
                ? $"Inventory check found {missingCount} missing items, {scannedCount} scanned. {document.DocumentNo}"
                : $"Inventory check completed without discrepancy. {document.DocumentNo}",
            Message_Zh = hasIssues
                ? $"盘点发现 {missingCount} 件缺失, {scannedCount} 件已扫描. {document.DocumentNo}"
                : $"盘点完成，未发现差异。{document.DocumentNo}",
            LinkUrl = "/?screen=inventory-check", CreatedAt = now, CreatedBy = user.UserName
        });
    }

    // ─── Progress ─────────────────────────────────────────────────────────────

    /// <summary>Trả về thống kê tiến độ phiên kiểm kê (dùng cho GET Progress/{id}).</summary>
    public async Task<object?> GetSessionProgressAsync(int documentId, CancellationToken cancellationToken = default)
    {
        var document = await _db.InventoryCheckDocuments
            .Include(x => x.Warehouse)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == documentId, cancellationToken);
        if (document == null) return null;

        var lines = await _db.InventoryCheckLines.AsNoTracking()
            .Where(x => x.InventoryCheckDocumentId == documentId)
            .GroupBy(x => x.Result)
            .Select(g => new { result = g.Key.ToString(), count = g.Count() })
            .ToListAsync(cancellationToken);

        return new
        {
            documentId = document.Id,
            documentNo = document.DocumentNo,
            warehouse = document.Warehouse?.WarehouseCode,
            sessionStatus = document.SessionStatus,
            documentDate = document.DocumentDate,
            responsibleStaff = document.ResponsibleStaff,
            summary = lines,
            totalScanned = lines.Where(x => x.result != "Missing").Sum(x => x.count),
            totalMissing = lines.Where(x => x.result == "Missing").Sum(x => x.count)
        };
    }
}
