using ERP.Inventory.Application.Common;
using ERP.Inventory.Application.DTOs;
using ERP.Inventory.Application.Interfaces;
using ERP.Inventory.Domain.Entities;
using ERP.Inventory.Domain.Enums;
using ERP.Inventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Net.NetworkInformation;

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

    public DocumentPeriodType InferPeriodType( string documentNo, DateTime sessionDate)
    {
        if (string.IsNullOrWhiteSpace(documentNo))
            return DocumentPeriodType.Month;

        var parts = documentNo.Split('/');

        if (parts.Length >= 3)
        {
            var period = parts[2];

            if (period.StartsWith("W", StringComparison.OrdinalIgnoreCase))
                return DocumentPeriodType.Week;

            if (period.StartsWith("M", StringComparison.OrdinalIgnoreCase))
                return DocumentPeriodType.Month;

            if (period.StartsWith("Q", StringComparison.OrdinalIgnoreCase))
                return DocumentPeriodType.Quarter;
        }

        return DocumentPeriodType.Year;
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
        var scanLines = request.Lines.ToArray();
        if (!scanLines.Any()) return ServiceResult<ScanBatchResultDto>.Fail("At least one scan line is required.");

        var warehouse = document.Warehouse!;

        // Validate lines trước
        foreach (var line in scanLines)
        {
            if (string.IsNullOrWhiteSpace(line.ItemCode)) return ServiceResult<ScanBatchResultDto>.Fail("ItemCode is required for every check line.");
            if (string.IsNullOrWhiteSpace(line.SerialNumber)) return ServiceResult<ScanBatchResultDto>.Fail("SerialNumber is required for every check line.");
            if (string.IsNullOrWhiteSpace(line.BinCode)) return ServiceResult<ScanBatchResultDto>.Fail("BinCode is required for every check line.");
        }

        var binCodes = scanLines.Select(x => x.BinCode.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var actualBinRows = new List<BinLocation>();
        foreach (var chunk in BatchLookup(binCodes))
        {
            actualBinRows.AddRange(await _db.BinLocations
                .AsNoTracking()
                .Where(x =>
                    x.IsActive &&
                    x.WarehouseId == document.WarehouseId &&
                    x.UsageType == BinLocationUsageType.LocationTracked &&
                    chunk.Contains(x.BinCode))
                .ToListAsync(cancellationToken));
        }
        var actualBins = actualBinRows.ToDictionary(x => LookupKey(x.BinCode));
        foreach (var line in scanLines)
        {
            if (!actualBins.ContainsKey(LookupKey(line.BinCode)))
            {
                return ServiceResult<ScanBatchResultDto>.Fail($"BinCode {line.BinCode} not found or is not a location-tracked bin in this warehouse.");
            }
        }

        var itemCodes = scanLines.Select(x => x.ItemCode.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var serialNumbers = scanLines.Select(x => x.SerialNumber.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var itemRows = new List<Item>();
        foreach (var chunk in BatchLookup(itemCodes))
        {
            itemRows.AddRange(await _db.Items
                .AsNoTracking()
                .Where(x => x.IsActive && chunk.Contains(x.ItemCode))
                .ToListAsync(cancellationToken));
        }
        var itemsByCode = itemRows.ToDictionary(x => LookupKey(x.ItemCode));
        var instances = new List<ItemInstance>();
        foreach (var itemChunk in BatchLookup(itemCodes, 500))
        {
            foreach (var serialChunk in BatchLookup(serialNumbers, 500))
            {
                instances.AddRange(await _db.ItemInstances
                    .Include(x => x.Item)
                    .Where(x =>
                        x.IsActive &&
                        !x.IsDeleted &&
                        x.TrackingType == ItemTrackingType.LocationTracked &&
                        x.Item != null &&
                        itemChunk.Contains(x.Item.ItemCode) &&
                        x.SerialNumber != null &&
                        serialChunk.Contains(x.SerialNumber))
                    .ToListAsync(cancellationToken));
            }
        }
        var instancesByItemSerial = instances
            .Where(x => x.Item != null && !string.IsNullOrWhiteSpace(x.SerialNumber))
            .GroupBy(x => (LookupKey(x.Item!.ItemCode), LookupKey(x.SerialNumber)))
            .ToDictionary(x => x.Key, x => x.First());

        var deletedInstanceKeys = new HashSet<(string ItemCode, string SerialNumber)>();
        foreach (var itemChunk in BatchLookup(itemCodes, 500))
        {
            foreach (var serialChunk in BatchLookup(serialNumbers, 500))
            {
                var deletedKeys = await _db.ItemInstances
                    .AsNoTracking()
                    .Include(x => x.Item)
                    .Where(x =>
                        x.IsDeleted &&
                        x.TrackingType == ItemTrackingType.LocationTracked &&
                        x.Item != null &&
                        itemChunk.Contains(x.Item.ItemCode) &&
                        x.SerialNumber != null &&
                        serialChunk.Contains(x.SerialNumber))
                    .Select(x => new { ItemCode = x.Item!.ItemCode, x.SerialNumber })
                    .ToListAsync(cancellationToken);

                foreach (var deletedKey in deletedKeys)
                {
                    deletedInstanceKeys.Add((LookupKey(deletedKey.ItemCode), LookupKey(deletedKey.SerialNumber)));
                }
            }
        }

        foreach (var line in scanLines)
        {
            var key = (LookupKey(line.ItemCode), LookupKey(line.SerialNumber));
            if (deletedInstanceKeys.Contains(key))
            {
                return ServiceResult<ScanBatchResultDto>.Fail($"Item {line.ItemCode} / {line.SerialNumber} has been deleted and cannot be used in inventory check.");
            }
        }

        var instanceIds = instances.Select(x => x.Id).Distinct().ToArray();
        var currentLocations = new List<CurrentItemLocation>();
        foreach (var chunk in BatchLookup(instanceIds))
        {
            currentLocations.AddRange(await _db.CurrentItemLocations
                .Include(x => x.BinLocation)
                .Where(x => chunk.Contains(x.ItemInstanceId) && !x.IsDeleted)
                .ToListAsync(cancellationToken));
        }
        var locationsByInstanceId = currentLocations.ToDictionary(x => x.ItemInstanceId);

        var stockWarehouseIds = currentLocations
            .Select(x => x.WarehouseId)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Append(warehouse.Id)
            .Distinct()
            .ToArray();
        var stockBinIds = currentLocations
            .Select(x => x.BinLocationId)
            .Concat(actualBins.Values.Select(x => (int?)x.Id))
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToArray();
        var stockItemIds = instances.Select(x => x.ItemId).Concat(itemsByCode.Values.Select(x => x.Id)).Distinct().ToArray();
        if (stockWarehouseIds.Length > 0 && stockBinIds.Length > 0 && stockItemIds.Length > 0)
        {
            foreach (var binChunk in BatchLookup(stockBinIds, 500))
            {
                foreach (var itemChunk in BatchLookup(stockItemIds, 500))
                {
                    await _db.StockBalances
                        .Where(x =>
                            stockWarehouseIds.Contains(x.WarehouseId) &&
                            x.BinLocationId.HasValue &&
                            binChunk.Contains(x.BinLocationId.Value) &&
                            itemChunk.Contains(x.ItemId))
                        .LoadAsync(cancellationToken);
                }
            }
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var now = _clock.UtcNow;

        // Lấy danh sách SerialNumber đã scan trong phiên này để tránh duplicate
        var alreadyScannedInstanceIds = (await _db.InventoryCheckLines
            .Where(x => x.InventoryCheckDocumentId == document.Id && x.Result != InventoryCheckLineResult.Missing && x.ItemInstanceId.HasValue)
            .Select(x => x.ItemInstanceId!.Value)
            .ToListAsync(cancellationToken)).ToHashSet();

        int matched = 0, wrongLocation = 0, extra = 0, skipped = 0;
        var pendingExtraEvents = new List<(ItemInstance Instance, Item Item, BinLocation Bin, string? Note)>();

        foreach (var line in scanLines)
        {
            var actualBin = actualBins[LookupKey(line.BinCode)];
            instancesByItemSerial.TryGetValue((LookupKey(line.ItemCode), LookupKey(line.SerialNumber)), out var instance);

            // INV-002 fix: duplicate validation is now item/serial-based via alreadyScannedInstanceIds,
            // not bin-based. Multiple serials in the same bin are now allowed.

            if (instance == null)
            {
                // === EXTRA: item không có trong DB → tạo mới ===
                if (!itemsByCode.TryGetValue(LookupKey(line.ItemCode), out var item))
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

                _db.CurrentItemLocations.Add(new CurrentItemLocation
                {
                    ItemInstance = newInstance, LocationType = LocationType.BinLocation,
                    WarehouseId = warehouse.Id, BinLocationId = actualBin.Id,
                    ReferenceDocumentType = nameof(InventoryCheckDocument), ReferenceDocumentId = document.Id,
                    ReferenceDocumentNo = document.DocumentNo,
                    UpdatedLocationAt = now, UpdatedLocationBy = user.UserName,
                    CreatedAt = now, CreatedBy = user.UserName
                });
                await ApplyStockDeltaAsync(warehouse.Id, actualBin.Id, item.Id, ItemStatus.Normal, 1, user, cancellationToken);
                pendingExtraEvents.Add((newInstance, item, actualBin, line.Note));

                _db.InventoryCheckLines.Add(new InventoryCheckLine
                {
                    InventoryCheckDocumentId = document.Id, ItemInstance = newInstance,
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

                locationsByInstanceId.TryGetValue(instance.Id, out var current);
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
                    if (instance.Status == ItemStatus.InStock || instance.Status == ItemStatus.Normal)
                        instance.Status = ItemStatus.Normal;
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

                        await ApplyStockDeltaAsync(warehouse.Id, actualBin.Id, instance.ItemId, instance.Status, 1, user, cancellationToken);
                        AddHistory(instance.Id, MovementActionType.MoveLocation, LocationType.BinLocation, oldBinId, $"Bin {current.BinLocation?.FullPath}", LocationType.BinLocation,
                            actualBin.Id, actualBin.FullPath, instance.Status, instance.Status, nameof(InventoryCheckDocument), document.Id, document.DocumentNo, "Inventory check: wrong location corrected", user);
                        AddInventoryTransaction(InventoryTransactionType.InventoryCheck, instance.ItemId, instance.Id, warehouse.Id, actualBin.Id, 0, instance.Status, nameof(InventoryCheckDocument), document.Id, document.DocumentNo, user);
                    }
                    wrongLocation++;
                }
            }
        }

        // Update SessionStatus vẫn là InProgress
        if (pendingExtraEvents.Count > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
            foreach (var pending in pendingExtraEvents)
            {
                AddHistory(pending.Instance.Id, MovementActionType.InventoryCheck, null, null, "Supplier", LocationType.BinLocation, pending.Bin.Id, pending.Bin.FullPath, ItemStatus.Reserved, ItemStatus.Normal, nameof(InventoryCheckDocument), document.Id, document.DocumentNo, pending.Note, user);
                AddInventoryTransaction(InventoryTransactionType.InventoryCheck, pending.Item.Id, pending.Instance.Id, warehouse.Id, pending.Bin.Id, 1, ItemStatus.Normal, nameof(InventoryCheckDocument), document.Id, document.DocumentNo, user);
            }
        }

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
            .Where(x => x.InventoryCheckDocumentId == documentId && x.Result != InventoryCheckLineResult.Missing && x.ItemInstanceId.HasValue 
            && x.ItemInstance != null && x.ItemInstance.IsActive && !x.ItemInstance.IsDeleted)
            .Select(x => x.ItemInstanceId!.Value).ToListAsync(cancellationToken)).ToHashSet();

        //var checkedBinIds = (await _db.InventoryCheckLines
        //    .AsNoTracking()
        //    .Where(x => x.InventoryCheckDocumentId == documentId && x.Result != InventoryCheckLineResult.Missing)
        //    .Select(x => new { x.SystemBinLocationId, x.ActualBinLocationId })
        //    .ToListAsync(cancellationToken))
        //    .SelectMany(x => new[] { x.SystemBinLocationId, x.ActualBinLocationId })
        //    .Where(x => x.HasValue)
        //    .Select(x => x!.Value)
        //    .Distinct()
        //    .ToArray();

        // === MISSING: items InStock trong kho chưa được scan ===
        var stockCandidateQuery = _db.CurrentItemLocations
            .Include(x => x.ItemInstance)
            .Include(x => x.BinLocation)
            .Where(x =>
                x.WarehouseId == document.WarehouseId &&
                !x.IsDeleted &&
                x.BinLocation != null &&
                x.BinLocation.UsageType == BinLocationUsageType.LocationTracked &&
                x.ItemInstance != null &&
                x.ItemInstance.IsActive &&
                !x.ItemInstance.IsDeleted &&
                x.ItemInstance.TrackingType == ItemTrackingType.LocationTracked &&
                (x.ItemInstance.Status == ItemStatus.InStock ||
                 x.ItemInstance.Status == ItemStatus.Normal ||
                 x.ItemInstance.Status == ItemStatus.Damaged ||
                 x.ItemInstance.Status == ItemStatus.Scrapped));

        var scannedInstanceIdArray = scannedInstanceIds.ToArray();
        var filterScannedInSql = scannedInstanceIdArray.Length <= 1800;
        if (filterScannedInSql)
        {
            stockCandidateQuery = stockCandidateQuery.Where(x => !scannedInstanceIdArray.Contains(x.ItemInstanceId));
        }

        //var filterCheckedBinsInSql = checkedBinIds.Length > 0 && checkedBinIds.Length <= 1800;
        //if (filterCheckedBinsInSql)
        //{
        //    stockCandidateQuery = stockCandidateQuery.Where(x => x.BinLocationId.HasValue && checkedBinIds.Contains(x.BinLocationId.Value));
        //}

        var stockCandidateLocations = (await stockCandidateQuery.ToListAsync(cancellationToken))
            .GroupBy(x => x.ItemInstanceId)
            .Select(x => x.First())
            .ToList();
        var allInStockLocations = filterScannedInSql
            ? stockCandidateLocations
            : stockCandidateLocations.Where(x => !scannedInstanceIds.Contains(x.ItemInstanceId)).ToList();
        //if (checkedBinIds.Length > 0 && !filterCheckedBinsInSql)
        //{
        //    var checkedBinSet = checkedBinIds.ToHashSet();
        //    allInStockLocations = allInStockLocations
        //        .Where(x => x.BinLocationId.HasValue && checkedBinSet.Contains(x.BinLocationId.Value))
        //        .ToList();
        //}

        var missingBinIds = allInStockLocations
            .Select(x => x.BinLocationId)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToArray();
        var missingItemIds = allInStockLocations
            .Select(x => x.ItemInstance!.ItemId)
            .Distinct()
            .ToArray();
        var missingStatuses = allInStockLocations
            .Select(x => x.ItemInstance!.Status)
            .Distinct()
            .ToArray();
        var stockBalanceMap = new Dictionary<(int WarehouseId, int? BinLocationId, int ItemId, ItemStatus Status), StockBalance>();
        if (missingBinIds.Length > 0 && missingItemIds.Length > 0)
        {
            foreach (var binChunk in BatchLookup(missingBinIds, 500))
            {
                foreach (var itemChunk in BatchLookup(missingItemIds, 500))
                {
                    var balances = await _db.StockBalances
                        .Where(x =>
                            x.WarehouseId == document.WarehouseId &&
                            x.BinLocationId.HasValue &&
                            binChunk.Contains(x.BinLocationId.Value) &&
                            itemChunk.Contains(x.ItemId) &&
                            missingStatuses.Contains(x.Status))
                        .ToListAsync(cancellationToken);
                    foreach (var balance in balances)
                    {
                        stockBalanceMap.TryAdd((balance.WarehouseId, balance.BinLocationId, balance.ItemId, balance.Status), balance);
                    }
                }
            }
        }

        foreach (var missingLoc in allInStockLocations)
        {
            var missingInstance = missingLoc.ItemInstance!;
            var oldStatus = missingInstance.Status;

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
                ApplyMissingStockDelta(oldWarehouseId.Value, oldBinId, missingInstance.ItemId, missingInstance.Status, -1);

            missingLoc.BinLocationId = null;
            missingLoc.ReferenceDocumentType = nameof(InventoryCheckDocument);
            missingLoc.ReferenceDocumentId = documentId;
            missingLoc.ReferenceDocumentNo = document.DocumentNo;
            missingLoc.UpdatedLocationAt = now;
            missingLoc.UpdatedLocationBy = user.UserName;

            if(missingInstance.Status == ItemStatus.Normal || missingInstance.Status == ItemStatus.InStock) missingInstance.Status = ItemStatus.Lost;
            AddHistory(missingInstance.Id, MovementActionType.InventoryCheck, LocationType.BinLocation, oldBinId, $"Bin {missingLoc.BinLocation?.FullPath}", null, null, "Unknown", oldStatus, missingInstance.Status, nameof(InventoryCheckDocument), documentId, document.DocumentNo, "Missing: not found during inventory check", user);
            AddInventoryTransaction(InventoryTransactionType.InventoryCheck, missingInstance.ItemId, missingInstance.Id, oldWarehouseId, oldBinId, -1, missingInstance.Status, nameof(InventoryCheckDocument), documentId, document.DocumentNo, user);
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

        void ApplyMissingStockDelta(int warehouseId, int? binLocationId, int itemId, ItemStatus status, decimal delta)
        {
            if (delta == 0) return;

            var key = (warehouseId, binLocationId, itemId, status);
            if (!stockBalanceMap.TryGetValue(key, out var balance))
            {
                balance = new StockBalance
                {
                    WarehouseId = warehouseId,
                    BinLocationId = binLocationId,
                    ItemId = itemId,
                    Status = status,
                    Quantity = 0,
                    CreatedAt = now,
                    CreatedBy = user.UserName
                };
                _db.StockBalances.Add(balance);
                stockBalanceMap[key] = balance;
            }

            balance.Quantity += delta;
            balance.UpdatedAt = now;
            balance.UpdatedBy = user.UserName;
        }
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static string LookupKey(string? value)
        => (value ?? string.Empty).Trim().ToUpperInvariant();

    private static IEnumerable<T[]> BatchLookup<T>(IReadOnlyCollection<T> values, int size = 900)
    {
        var buffer = new List<T>(size);
        foreach (var value in values)
        {
            buffer.Add(value);
            if (buffer.Count < size)
            {
                continue;
            }

            yield return buffer.ToArray();
            buffer.Clear();
        }

        if (buffer.Count > 0)
        {
            yield return buffer.ToArray();
        }
    }

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
