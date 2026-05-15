using ERP.Inventory.Application.Common;
using ERP.Inventory.Application.DTOs;
using ERP.Inventory.Application.Interfaces;
using ERP.Inventory.Domain.Entities;
using ERP.Inventory.Domain.Enums;
using ERP.Inventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ERP.Inventory.Infrastructure.Services;

/// <summary>
/// Reconciliation Audit Service — Đối soát tài sản.
/// So sánh Reference List vs ERP theo (ItemCode, SerialNumber).
/// </summary>
public sealed class ReconciliationService
{
    private readonly InventoryDbContext _db;
    private readonly IDocumentNumberService _documentNumbers;
    private readonly IDateTimeProvider _clock;

    public ReconciliationService(InventoryDbContext db, IDocumentNumberService documentNumbers, IDateTimeProvider clock)
    {
        _db = db;
        _documentNumbers = documentNumbers;
        _clock = clock;
    }

    // ─── Reference List CRUD ──────────────────────────────────────────────────

    public async Task<ServiceResult<int>> CreateReferenceListAsync(CreateReferenceListRequest request, CurrentUserContext user, CancellationToken ct = default)
    {
        var wh = await _db.Warehouses.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.WarehouseId && x.IsActive, ct);
        if (wh == null) return ServiceResult<int>.Fail("Warehouse not found.");
        if (!user.CanAccessWarehouse(request.WarehouseId)) return ServiceResult<int>.Fail("Permission denied for this warehouse.");
        if (string.IsNullOrWhiteSpace(request.ListCode)) return ServiceResult<int>.Fail("ListCode is required.");
        if (string.IsNullOrWhiteSpace(request.Name)) return ServiceResult<int>.Fail("Name is required.");

        var code = request.ListCode.Trim().ToUpper();
        if (await _db.ReferenceListHeaders.AnyAsync(x => x.ListCode == code, ct))
            return ServiceResult<int>.Fail($"ListCode '{code}' already exists.");

        var now = _clock.UtcNow;
        var header = new ReferenceListHeader
        {
            ListCode = code,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            WarehouseId = request.WarehouseId,
            IsActive = true,
            CreatedAt = now,
            CreatedBy = user.UserName
        };
        _db.ReferenceListHeaders.Add(header);
        await _db.SaveChangesAsync(ct);
        return ServiceResult<int>.Ok(header.Id, $"Reference list '{code}' created.");
    }

    public async Task<ServiceResult<IReadOnlyCollection<ReferenceListSummaryDto>>> GetReferenceListsAsync(CurrentUserContext user, CancellationToken ct = default)
    {
        var query = _db.ReferenceListHeaders.AsNoTracking()
            .Include(x => x.Warehouse)
            .Where(x => x.IsActive);

        if (!user.IsAdmin)
            query = query.Where(x => user.WarehouseIds.Contains(x.WarehouseId));

        var rows = await query.OrderByDescending(x => x.CreatedAt)
            .Select(x => new ReferenceListSummaryDto
            {
                Id = x.Id,
                ListCode = x.ListCode,
                Name = x.Name,
                Description = x.Description,
                WarehouseId = x.WarehouseId,
                WarehouseName = x.Warehouse != null ? x.Warehouse.Name : string.Empty,
                ItemCount = x.Items.Count(i => i.IsActive),
                IsActive = x.IsActive,
                CreatedAt = x.CreatedAt,
                CreatedBy = x.CreatedBy
            }).ToArrayAsync(ct);

        return ServiceResult<IReadOnlyCollection<ReferenceListSummaryDto>>.Ok(rows);
    }

    public async Task<ServiceResult<IReadOnlyCollection<ReferenceListItemDto>>> GetReferenceListItemsAsync(int listId, CurrentUserContext user, CancellationToken ct = default)
    {
        var header = await _db.ReferenceListHeaders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == listId, ct);
        if (header == null) return ServiceResult<IReadOnlyCollection<ReferenceListItemDto>>.Fail("Reference list not found.");
        if (!user.IsAdmin && !user.CanAccessWarehouse(header.WarehouseId))
            return ServiceResult<IReadOnlyCollection<ReferenceListItemDto>>.Fail("Permission denied.");

        var rows = await _db.ReferenceListItems.AsNoTracking()
            .Where(x => x.ReferenceListId == listId)
            .OrderBy(x => x.ItemCode).ThenBy(x => x.SerialNumber)
            .Select(x => new ReferenceListItemDto
            {
                Id = x.Id,
                ItemCode = x.ItemCode,
                SerialNumber = x.SerialNumber,
                ResolvedItemName = x.ResolvedItemName,
                IsActive = x.IsActive,
                ImportedAt = x.ImportedAt,
                ImportedBy = x.ImportedBy,
                LastUpdatedAt = x.LastUpdatedAt,
                Note = x.Note,
                IsResolvedInERP = x.ItemInstanceId.HasValue
            }).ToArrayAsync(ct);

        return ServiceResult<IReadOnlyCollection<ReferenceListItemDto>>.Ok(rows);
    }

    // ─── Import Reference List ────────────────────────────────────────────────

    /// <summary>Tạo file Excel template cho Reference List.</summary>
    public byte[] GetReferenceListTemplate(string lang)
    {
        string[] headers = lang switch
        {
            "zh" => new[] { "物料编码", "序列号", "备注" },
            "en" => new[] { "ItemCode", "SerialNumber", "Note" },
            _ => new[] { "ItemCode", "SerialNumber", "GhiChu" }
        };
        var emptyRows = Array.Empty<IReadOnlyCollection<object?>>();
        return SimpleExcel.CreateWorkbook(headers, emptyRows, "ReferenceList");
    }

    /// <summary>
    /// Overload nhận Stream + fileName — parse Excel rồi gọi ImportReferenceListAsync.
    /// Dùng từ Controller để tránh expose SimpleExcel ra Web layer.
    /// </summary>
    public async Task<ServiceResult<ImportReferenceListResultDto>> ImportReferenceListFromStreamAsync(
        int listId,
        ReferenceListImportMode importMode,
        Stream fileStream,
        string fileName,
        CurrentUserContext user,
        CancellationToken ct = default)
    {
        IReadOnlyCollection<Dictionary<string, string>> rows;
        try
        {
            rows = await SimpleExcel.ReadTableAsync(fileStream, fileName, ct);
        }
        catch (Exception ex)
        {
            return ServiceResult<ImportReferenceListResultDto>.Fail($"File parse error: {ex.Message}");
        }

        if (!rows.Any())
            return ServiceResult<ImportReferenceListResultDto>.Fail("File does not contain data rows.");

        return await ImportReferenceListAsync(listId, importMode, rows, user, ct);
    }


    /// Import Excel vào Reference List.
    /// rows: list of {ItemCode, SerialNumber, Note} parsed từ Excel (key case-insensitive).
    /// ImportMode: Supplement (upsert) | Replace (xóa all rồi insert lại).
    /// </summary>
    public async Task<ServiceResult<ImportReferenceListResultDto>> ImportReferenceListAsync(
        int listId,
        ReferenceListImportMode importMode,
        IReadOnlyCollection<Dictionary<string, string>> rows,
        CurrentUserContext user,
        CancellationToken ct = default)
    {
        var header = await _db.ReferenceListHeaders.FirstOrDefaultAsync(x => x.Id == listId, ct);
        if (header == null) return ServiceResult<ImportReferenceListResultDto>.Fail("Reference list not found.");
        if (!user.IsAdmin && !user.CanAccessWarehouse(header.WarehouseId))
            return ServiceResult<ImportReferenceListResultDto>.Fail("Permission denied.");
        if (!rows.Any()) return ServiceResult<ImportReferenceListResultDto>.Fail("File does not contain data rows.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var now = _clock.UtcNow;
        int inserted = 0, updated = 0, deleted = 0, unresolved = 0;

        // Mode B: Replace — xóa toàn bộ cũ trước
        if (importMode == ReferenceListImportMode.Replace)
        {
            var oldItems = await _db.ReferenceListItems.Where(x => x.ReferenceListId == listId).ToListAsync(ct);
            deleted = oldItems.Count;
            _db.ReferenceListItems.RemoveRange(oldItems);
            await _db.SaveChangesAsync(ct);

            // Ghi audit log cho Replace mode
            _db.AuditLogs.Add(new AuditLog
            {
                UserId = user.UserId, UserName = user.UserName,
                Action = "ReplaceImport", EntityName = nameof(ReferenceListHeader),
                EntityId = listId, ReferenceNo = header.ListCode,
                BeforeJson = $"{{\"deletedCount\":{deleted}}}",
                Result = "Success", CreatedAt = now
            });
        }

        // Build lookup map cho Supplement mode
        Dictionary<(string, string?), ReferenceListItem>? existingMap = null;
        if (importMode == ReferenceListImportMode.Supplement)
        {
            var existing = await _db.ReferenceListItems.Where(x => x.ReferenceListId == listId).ToListAsync(ct);
            existingMap = existing.ToDictionary(
                x => (x.ItemCode.ToUpperInvariant(), x.SerialNumber?.Trim()),
                x => x);
        }

        foreach (var row in rows)
        {
            var itemCode = GetVal(row, "ItemCode")?.Trim().ToUpperInvariant();
            var serial = GetVal(row, "SerialNumber")?.Trim();
            var note = GetVal(row, "Note")?.Trim();

            if (string.IsNullOrWhiteSpace(itemCode)) continue;

            // Resolve ItemInstance từ ERP
            var instance = await _db.ItemInstances
                .Include(x => x.Item)
                .FirstOrDefaultAsync(x => x.Item != null && x.Item.ItemCode == itemCode
                    && x.SerialNumber == serial, ct);
            if (instance == null) unresolved++;

            var key = (itemCode, string.IsNullOrWhiteSpace(serial) ? null : serial);

            if (importMode == ReferenceListImportMode.Supplement && existingMap!.TryGetValue(key, out var existing))
            {
                // Upsert — cập nhật metadata
                existing.LastUpdatedAt = now;
                existing.IsActive = true;
                if (!string.IsNullOrWhiteSpace(note)) existing.Note = note;
                if (instance != null)
                {
                    existing.ItemInstanceId = instance.Id;
                    existing.ItemId = instance.ItemId;
                    existing.ResolvedItemName = instance.Item?.DefaultName;
                }
                updated++;
            }
            else
            {
                // Insert mới
                _db.ReferenceListItems.Add(new ReferenceListItem
                {
                    ReferenceListId = listId,
                    ItemCode = itemCode,
                    SerialNumber = string.IsNullOrWhiteSpace(serial) ? null : serial,
                    ItemInstanceId = instance?.Id,
                    ItemId = instance?.ItemId,
                    ResolvedItemName = instance?.Item?.DefaultName,
                    IsActive = true,
                    ImportedAt = now,
                    ImportedBy = user.UserName,
                    Note = string.IsNullOrWhiteSpace(note) ? null : note,
                    CreatedAt = now, CreatedBy = user.UserName
                });
                inserted++;
            }
        }

        header.UpdatedAt = now;
        header.UpdatedBy = user.UserName;

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return ServiceResult<ImportReferenceListResultDto>.Ok(new ImportReferenceListResultDto
        {
            ReferenceListId = listId,
            ImportMode = importMode.ToString(),
            Inserted = inserted,
            Updated = updated,
            Deleted = deleted,
            UnresolvedInERP = unresolved,
            TotalProcessed = inserted + updated
        }, $"Import completed: {inserted} inserted, {updated} updated, {unresolved} unresolved in ERP.");
    }

    // ─── Reconciliation Session ───────────────────────────────────────────────

    public async Task<ServiceResult<ReconciliationSessionSummaryDto>> CreateSessionAsync(
        CreateReconciliationSessionRequest request, CurrentUserContext user, CancellationToken ct = default)
    {
        var refList = await _db.ReferenceListHeaders.AsNoTracking()
            .Include(x => x.Warehouse)
            .FirstOrDefaultAsync(x => x.Id == request.ReferenceListId && x.IsActive, ct);
        if (refList == null) return ServiceResult<ReconciliationSessionSummaryDto>.Fail("Reference list not found.");
        if (!user.IsAdmin && !user.CanAccessWarehouse(refList.WarehouseId))
            return ServiceResult<ReconciliationSessionSummaryDto>.Fail("Permission denied.");

        var now = _clock.UtcNow;
        var sessionNo = _documentNumbers.Next("REC", now);

        var session = new ReconciliationSession
        {
            SessionNo = sessionNo,
            ReferenceListId = refList.Id,
            WarehouseId = refList.WarehouseId,
            SessionStatus = nameof(ReconciliationSessionStatus.Draft),
            Note = request.Note?.Trim(),
            CreatedAt = now, CreatedBy = user.UserName
        };
        _db.ReconciliationSessions.Add(session);
        await _db.SaveChangesAsync(ct);

        return ServiceResult<ReconciliationSessionSummaryDto>.Ok(
            MapSessionDto(session, refList.Name, refList.Warehouse?.Name ?? string.Empty),
            $"Session {sessionNo} created.");
    }

    public async Task<ServiceResult<ReconciliationSessionSummaryDto>> RunSessionAsync(
        int sessionId, CurrentUserContext user, CancellationToken ct = default)
    {
        var session = await _db.ReconciliationSessions
            .Include(x => x.ReferenceList).ThenInclude(x => x!.Warehouse)
            .FirstOrDefaultAsync(x => x.Id == sessionId, ct);
        if (session == null) return ServiceResult<ReconciliationSessionSummaryDto>.Fail("Session not found.");
        if (session.SessionStatus == nameof(ReconciliationSessionStatus.Running))
            return ServiceResult<ReconciliationSessionSummaryDto>.Fail("Session is already running.");
        if (session.SessionStatus == nameof(ReconciliationSessionStatus.Completed))
            return ServiceResult<ReconciliationSessionSummaryDto>.Fail("Session already completed. Create a new session to run again.");
        if (!user.IsAdmin && !user.CanAccessWarehouse(session.WarehouseId))
            return ServiceResult<ReconciliationSessionSummaryDto>.Fail("Permission denied.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var now = _clock.UtcNow;

        // Xóa kết quả cũ nếu có (re-run Draft session)
        var oldResults = await _db.ReconciliationResults.Where(x => x.SessionId == sessionId).ToListAsync(ct);
        if (oldResults.Any()) _db.ReconciliationResults.RemoveRange(oldResults);

        session.SessionStatus = nameof(ReconciliationSessionStatus.Running);
        session.RunAt = now;
        await _db.SaveChangesAsync(ct);

        // ── Step 1: Load ERP snapshot (scope theo Warehouse) ──────────────────
        // Join ItemInstances + Items + CurrentItemLocation
        var erpItems = await _db.CurrentItemLocations.AsNoTracking()
            .Include(x => x.ItemInstance).ThenInclude(x => x!.Item)
            .Include(x => x.BinLocation)
            .Include(x => x.ExternalParty)
            .Where(x => x.WarehouseId == session.WarehouseId
                && x.ItemInstance != null && x.ItemInstance.IsActive
                && x.ItemInstance.Item != null)
            .Select(x => new
            {
                ItemCode = x.ItemInstance!.Item!.ItemCode,
                SerialNumber = x.ItemInstance.SerialNumber,
                ItemInstanceId = x.ItemInstance.Id,
                ItemName = x.ItemInstance.Item.DefaultName,
                Status = x.ItemInstance.Status,
                LocationText = x.BinLocation != null ? x.BinLocation.FullPath
                    : x.ExternalParty != null ? x.ExternalParty.Name
                    : x.LocationType.ToString()
            })
            .ToListAsync(ct);

        // Build ERP dictionary: key = (ItemCode.Upper, SerialNumber)
        var erpMap = erpItems
            .GroupBy(x => (x.ItemCode.ToUpperInvariant(), x.SerialNumber?.Trim()))
            .ToDictionary(g => g.Key, g => g.First());

        // ── Step 2: Load Reference List ────────────────────────────────────────
        var refItems = await _db.ReferenceListItems.AsNoTracking()
            .Where(x => x.ReferenceListId == session.ReferenceListId && x.IsActive)
            .ToListAsync(ct);

        var refMap = refItems
            .GroupBy(x => (x.ItemCode.ToUpperInvariant(), x.SerialNumber?.Trim()))
            .ToDictionary(g => g.Key, g => g.First());

        // ── Step 3: Classify ───────────────────────────────────────────────────
        var results = new List<ReconciliationResult>();
        var processedErpKeys = new HashSet<(string, string?)>();

        // Duyệt ref list → Matched hoặc RefOnly
        foreach (var (key, refItem) in refMap)
        {
            if (erpMap.TryGetValue(key, out var erpItem))
            {
                processedErpKeys.Add(key);
                var isLentOut = erpItem.Status == ItemStatus.LentOut;
                results.Add(new ReconciliationResult
                {
                    SessionId = sessionId,
                    ItemCode = refItem.ItemCode,
                    SerialNumber = refItem.SerialNumber,
                    ItemInstanceId = erpItem.ItemInstanceId,
                    ResolvedItemName = erpItem.ItemName,
                    ResultType = ReconciliationResultType.Matched,
                    ErpStatus = erpItem.Status.ToString(),
                    ErpLocationText = erpItem.LocationText,
                    Note = isLentOut ? "Đang mượn / On loan / 借出中" : null,
                    RefListItemId = refItem.Id,
                    CreatedAt = now
                });
            }
            else
            {
                results.Add(new ReconciliationResult
                {
                    SessionId = sessionId,
                    ItemCode = refItem.ItemCode,
                    SerialNumber = refItem.SerialNumber,
                    ResolvedItemName = refItem.ResolvedItemName,
                    ResultType = ReconciliationResultType.RefOnly,
                    RefListItemId = refItem.Id,
                    CreatedAt = now
                });
            }
        }

        // Duyệt ERP → ERPOnly (những item chưa được xử lý)
        foreach (var (key, erpItem) in erpMap)
        {
            if (processedErpKeys.Contains(key)) continue;
            results.Add(new ReconciliationResult
            {
                SessionId = sessionId,
                ItemCode = erpItem.ItemCode,
                SerialNumber = erpItem.SerialNumber,
                ItemInstanceId = erpItem.ItemInstanceId,
                ResolvedItemName = erpItem.ItemName,
                ResultType = ReconciliationResultType.ERPOnly,
                ErpStatus = erpItem.Status.ToString(),
                ErpLocationText = erpItem.LocationText,
                CreatedAt = now
            });
        }

        _db.ReconciliationResults.AddRange(results);

        // Update session summary
        session.SessionStatus = nameof(ReconciliationSessionStatus.Completed);
        session.CompletedAt = now;
        session.UpdatedAt = now;
        session.UpdatedBy = user.UserName;
        session.TotalRef = refMap.Count;
        session.TotalErp = erpMap.Count;
        session.MatchedCount = results.Count(x => x.ResultType == ReconciliationResultType.Matched);
        session.ERPOnlyCount = results.Count(x => x.ResultType == ReconciliationResultType.ERPOnly);
        session.RefOnlyCount = results.Count(x => x.ResultType == ReconciliationResultType.RefOnly);

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = user.UserId, UserName = user.UserName,
            Action = "RunReconciliation", EntityName = nameof(ReconciliationSession),
            EntityId = sessionId, ReferenceNo = session.SessionNo,
            AfterJson = JsonSerializer.Serialize(new { session.MatchedCount, session.ERPOnlyCount, session.RefOnlyCount }),
            Result = "Success", CreatedAt = now
        });

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return ServiceResult<ReconciliationSessionSummaryDto>.Ok(
            MapSessionDto(session, session.ReferenceList?.Name ?? string.Empty,
                session.ReferenceList?.Warehouse?.Name ?? string.Empty),
            $"Reconciliation completed: {session.MatchedCount} matched, {session.ERPOnlyCount} ERP only, {session.RefOnlyCount} ref only.");
    }

    public async Task<ServiceResult<IReadOnlyCollection<ReconciliationSessionSummaryDto>>> GetSessionsAsync(
        CurrentUserContext user, CancellationToken ct = default)
    {
        var query = _db.ReconciliationSessions.AsNoTracking()
            .Include(x => x.ReferenceList).ThenInclude(x => x!.Warehouse)
            .Where(x => x.SessionStatus != nameof(ReconciliationSessionStatus.Archived));

        if (!user.IsAdmin)
            query = query.Where(x => user.WarehouseIds.Contains(x.WarehouseId));

        var rows = await query.OrderByDescending(x => x.CreatedAt).Take(100)
            .ToListAsync(ct);

        var dtos = rows.Select(x =>
            MapSessionDto(x, x.ReferenceList?.Name ?? string.Empty,
                x.ReferenceList?.Warehouse?.Name ?? string.Empty)).ToArray();

        return ServiceResult<IReadOnlyCollection<ReconciliationSessionSummaryDto>>.Ok(dtos);
    }

    public async Task<ServiceResult<ReconciliationSessionResultDto>> GetSessionResultsAsync(
        int sessionId, ReconciliationResultFilter filter, CurrentUserContext user, CancellationToken ct = default)
    {
        var session = await _db.ReconciliationSessions.AsNoTracking()
            .Include(x => x.ReferenceList).ThenInclude(x => x!.Warehouse)
            .FirstOrDefaultAsync(x => x.Id == sessionId, ct);
        if (session == null) return ServiceResult<ReconciliationSessionResultDto>.Fail("Session not found.");
        if (!user.IsAdmin && !user.CanAccessWarehouse(session.WarehouseId))
            return ServiceResult<ReconciliationSessionResultDto>.Fail("Permission denied.");

        var query = _db.ReconciliationResults.AsNoTracking()
            .Where(x => x.SessionId == sessionId);

        if (!string.IsNullOrWhiteSpace(filter.ResultType) &&
            Enum.TryParse<ReconciliationResultType>(filter.ResultType, true, out var rt))
            query = query.Where(x => x.ResultType == rt);

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var kw = filter.Keyword.Trim();
            query = query.Where(x => x.ItemCode.Contains(kw) ||
                (x.SerialNumber != null && x.SerialNumber.Contains(kw)));
        }

        var total = await query.CountAsync(ct);
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 10, 200);

        var rows = await query.OrderBy(x => x.ResultType).ThenBy(x => x.ItemCode)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new ReconciliationResultRowDto
            {
                Id = x.Id,
                ItemCode = x.ItemCode,
                SerialNumber = x.SerialNumber,
                //ResolvedItemName = x.ResolvedItemName,
                ResultType = x.ResultType.ToString(),
                ErpStatus = x.ErpStatus,
                ErpLocationText = x.ErpLocationText,
                //Note = x.Note
            }).ToArrayAsync(ct);

        var sessionDto = MapSessionDto(session, session.ReferenceList?.Name ?? string.Empty,
            session.ReferenceList?.Warehouse?.Name ?? string.Empty);

        return ServiceResult<ReconciliationSessionResultDto>.Ok(new ReconciliationSessionResultDto
        {
            Session = sessionDto,
            Results = rows,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        });
    }

    // ─── Export Excel ─────────────────────────────────────────────────────────

    public async Task<byte[]> ExportSessionResultAsync(int sessionId, ReconciliationResultFilter filter, CurrentUserContext user, CancellationToken ct = default)
    {
        var session = await _db.ReconciliationSessions.AsNoTracking()
            .Include(x => x.ReferenceList)
            .FirstOrDefaultAsync(x => x.Id == sessionId, ct);
        if (session == null) return Array.Empty<byte>();

        var query = _db.ReconciliationResults.AsNoTracking().Where(x => x.SessionId == sessionId);

        if (!string.IsNullOrWhiteSpace(filter.ResultType) &&
            Enum.TryParse<ReconciliationResultType>(filter.ResultType, true, out var rt))
            query = query.Where(x => x.ResultType == rt);

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var kw = filter.Keyword.Trim();
            query = query.Where(x => x.ItemCode.Contains(kw) ||
                (x.SerialNumber != null && x.SerialNumber.Contains(kw)));
        }

        var rows = await query.OrderBy(x => x.ResultType).ThenBy(x => x.ItemCode)
            .Select(x => new object?[]
            {
                x.ItemCode,
                x.SerialNumber,
                //x.ResolvedItemName,
                ExcelText(user, x.ResultType.ToString()),
                ExcelText(user,x.ErpStatus),
                x.ErpLocationText,
                
            })
            .ToArrayAsync(ct);

        // Cast to IReadOnlyCollection<IReadOnlyCollection<object?>> for SimpleExcel
        var excelRows = rows.Select(r => (IReadOnlyCollection<object?>)r).ToArray();

        var lang = NormalizeLanguage(user);
        string[] headers = lang switch
        {
            "zh" => new[] { "PN", "SN", "对账结果", "状态", "位置" },
            "en" => new[] { "PN", "SN", "ResultType", "Status", "Location"},
            _ => new[] { "PN", "SN", "Kết quả", "Trạng thái", "Vị trí "}
        };

        var sheetName = lang switch
        {
            "zh" => $"对账_{session.SessionNo}",
            "en" => $"Recon_{session.SessionNo}",
            _ => $"DoiSoat_{session.SessionNo}"
        };

        return SimpleExcel.CreateWorkbook(headers, excelRows, sheetName);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static ReconciliationSessionSummaryDto MapSessionDto(
        ReconciliationSession s, string listName, string warehouseName) => new()
        {
            Id = s.Id,
            SessionNo = s.SessionNo,
            ReferenceListId = s.ReferenceListId,
            ReferenceListName = listName,
            WarehouseId = s.WarehouseId,
            WarehouseName = warehouseName,
            SessionStatus = s.SessionStatus,
            RunAt = s.RunAt,
            CompletedAt = s.CompletedAt,
            TotalRef = s.TotalRef,
            TotalErp = s.TotalErp,
            MatchedCount = s.MatchedCount,
            ERPOnlyCount = s.ERPOnlyCount,
            RefOnlyCount = s.RefOnlyCount,
            CreatedAt = s.CreatedAt,
            CreatedBy = s.CreatedBy,
            Note = s.Note
        };

    private static string? GetVal(Dictionary<string, string> row, string key)
    {
        var match = row.Keys.FirstOrDefault(k => k.Equals(key, StringComparison.OrdinalIgnoreCase));
        return match != null ? row[match] : null;
    }
    private static string ExcelText(CurrentUserContext user, string? key)
    {
        var language = NormalizeLanguage(user);
        if(string.IsNullOrEmpty(key))  return "";
        return ExcelResources.TryGetValue(language, out var resources) && resources.TryGetValue(key, out var value) ? value : key;
    }

    private static string NormalizeLanguage(CurrentUserContext user)
    {
        return user.LanguageCode?.Trim().ToLowerInvariant() switch
        {
            "en" => "en",
            "zh" => "zh",
            _ => "vi"
        };
    }

    private static readonly Dictionary<string, Dictionary<string, string>> ExcelResources = new()
    {
        ["vi"] = new()
        {
            ["Matched"] = "Khớp",
            ["ERPOnly"] = "Chỉ có trên hệ thống",
            ["RefOnly"] = "Chỉ trong danh sách tham chiếu",
            ["InStock"] = "Trong kho",
            ["Normal"] = "Bình thường",
            ["Reserved"] = "Đã giữ chỗ",
            ["Repairing"] = "Đang sửa chữa",
            ["LentOut"] = "Đã cho mượn",
            ["Returned"] = "Đã trả",
            ["Damaged"] = "Hư hỏng",
            ["Lost"] = "Thất lạc",
            ["Disposed"] = "Đã thanh lý",
            ["InTransit"] = "Đang vận chuyển",
            ["Replacement"] = "Thay thế serial",
            ["Scrapped"] = "Báo phế",
            ["Missing"] = "Thiếu",
            ["Extra"] = "Thừa",
            ["WrongLocation"] = "Sai vị trí",
        },
        ["en"] = new()
        {
            ["Matched"] = "Matched",
            ["ERPOnly"] = "Only available in the system",
            ["RefOnly"] = "Reference list only",
            ["InStock"] = "In stock",
            ["Normal"] = "Normal",
            ["Reserved"] = "Reserved",
            ["Repairing"] = "Repairing",
            ["LentOut"] = "Lent out",
            ["Returned"] = "Returned",
            ["Damaged"] = "Damaged",
            ["Lost"] = "Lost",
            ["Disposed"] = "Disposed",
            ["InTransit"] = "In transit",
            ["Replacement"] = "Replacement",
            ["Scrapped"] = "Scrapped",
            ["Missing"] = "Missing",
            ["Extra"] = "Extra",
            ["WrongLocation"] = "Wrong location",
        },
        ["zh"] = new()
        {
            ["ERPOnly"] = "仅存在于系统中",
            ["RefOnly"] = "仅存在于参考列表中",
            ["Matched"] = "匹配",
            ["InStock"] = "在库",
            ["Normal"] = "通过",
            ["Reserved"] = "已预留",
            ["Repairing"] = "维修中",
            ["LentOut"] = "已借出",
            ["Returned"] = "已归还",
            ["Damaged"] = "损坏",
            ["Lost"] = "丢失",
            ["Disposed"] = "已报废",
            ["InTransit"] = "运输中",
            ["Replacement"] = "替换",
            ["Scrapped"] = "报废",
            ["Missing"] = "缺失",
            ["Extra"] = "多余",
            ["WrongLocation"] = "位置错误",
        }
    };
 }

