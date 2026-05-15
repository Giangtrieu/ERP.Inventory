using ERP.Inventory.Application.Interfaces;
using ERP.Inventory.Domain.Entities;
using ERP.Inventory.Infrastructure.Data;
using ERP.Inventory.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ERP.Inventory.Web.Controllers;

[Authorize]
[Route("[controller]")]
public sealed class DocumentsController : Controller
{
    private readonly InventoryDbContext _db;
    private readonly ICurrentUserService _currentUserService;

    public DocumentsController(InventoryDbContext db, ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }

    [HttpGet("List")]
    public async Task<IActionResult> List([FromQuery] string type, [FromQuery] string? keyword, [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate, CancellationToken cancellationToken)
    {
        var language = Language();
        var rows = type switch
        {
            "inbound" => await InboundRows(keyword, fromDate, toDate, language, cancellationToken),
            "move" => await MoveRows(keyword, fromDate, toDate, language, cancellationToken),
            "adjustment" => await AdjustmentRows(keyword, fromDate, toDate, language, cancellationToken),
            "inventory-check" => await InventoryCheckRows(keyword, fromDate, toDate, language, cancellationToken),
            "repair-send" or "repair-receive" => await RepairRows(keyword, fromDate, toDate, language, cancellationToken),
            "borrow-lend" or "borrow-return" => await BorrowRows(keyword, fromDate, toDate, language, cancellationToken),
            _ => Array.Empty<object>()
        };

        return Json(rows);
    }

    [HttpGet("Detail")]
    public async Task<IActionResult> Detail([FromQuery] string type, [FromQuery] int id, CancellationToken cancellationToken)
    {
        var language = Language();
        object? detail = type switch
        {
            "inbound" => await InboundDetail(id, language, cancellationToken),
            "move" => await MoveDetail(id, language, cancellationToken),
            "adjustment" => await AdjustmentDetail(id, language, cancellationToken),
            "inventory-check" => await InventoryCheckDetail(id, language, cancellationToken),
            "repair-send" or "repair-receive" => await RepairDetail(id, language, cancellationToken),
            "borrow-lend" or "borrow-return" => await BorrowDetail(id, language, cancellationToken),
            _ => null
        };

        return detail == null ? NotFound() : Json(detail);
    }

    // ─── List queries ────────────────────────────────────────

    private async Task<object[]> InboundRows(string? keyword, DateTime? fromDate, DateTime? toDate, string language, CancellationToken cancellationToken)
    {
        var query = Scope(_db.InboundDocuments.AsNoTracking().Include(x => x.Warehouse).Include(x => x.SourceExternalParty).Include(x => x.Lines).AsQueryable(), x => x.WarehouseId);
        query = ApplyDocumentFilter(query, keyword, fromDate, toDate);
        return await query.OrderByDescending(x => x.DocumentDate).ThenByDescending(x => x.PostedAt).Take(100)
            .Select(x => new { id = x.Id, documentNo = x.DocumentNo, documentDate = x.DocumentDate, party = x.SourceExternalParty != null ? x.SourceExternalParty.Name : "", warehouse = x.Warehouse != null ? x.Warehouse.WarehouseCode : "", status = LocalizationCatalog.EnumText(language, x.Status), lines = x.Lines.Count, createdBy = x.CreatedBy, approvedBy = x.ApprovedBy, postedAt = x.PostedAt })
            .ToArrayAsync(cancellationToken);
    }

    private async Task<object[]> MoveRows(string? keyword, DateTime? fromDate, DateTime? toDate, string language, CancellationToken cancellationToken)
    {
        var query = Scope(_db.MoveDocuments.AsNoTracking().Include(x => x.Warehouse).Include(x => x.Lines).AsQueryable(), x => x.WarehouseId);
        query = ApplyDocumentFilter(query, keyword, fromDate, toDate);
        return await query.OrderByDescending(x => x.DocumentDate).ThenByDescending(x => x.PostedAt).Take(100)
            .Select(x => new { id = x.Id, documentNo = x.DocumentNo, documentDate = x.DocumentDate, party = "", warehouse = x.Warehouse != null ? x.Warehouse.WarehouseCode : "", status = LocalizationCatalog.EnumText(language, x.Status), lines = x.Lines.Count, createdBy = x.CreatedBy, approvedBy = x.ApprovedBy, postedAt = x.PostedAt })
            .ToArrayAsync(cancellationToken);
    }

    private async Task<object[]> AdjustmentRows(string? keyword, DateTime? fromDate, DateTime? toDate, string language, CancellationToken cancellationToken)
    {
        var query = Scope(_db.AdjustmentDocuments.AsNoTracking().Include(x => x.Warehouse).Include(x => x.Lines).AsQueryable(), x => x.WarehouseId);
        query = ApplyDocumentFilter(query, keyword, fromDate, toDate);
        return await query.OrderByDescending(x => x.DocumentDate).ThenByDescending(x => x.PostedAt).Take(100)
            .Select(x => new { id = x.Id, documentNo = x.DocumentNo, documentDate = x.DocumentDate, party = x.Reason, warehouse = x.Warehouse != null ? x.Warehouse.WarehouseCode : "", status = LocalizationCatalog.EnumText(language, x.Status), lines = x.Lines.Count, createdBy = x.CreatedBy, approvedBy = x.ApprovedBy, postedAt = x.PostedAt })
            .ToArrayAsync(cancellationToken);
    }

    private async Task<object[]> InventoryCheckRows(string? keyword, DateTime? fromDate, DateTime? toDate, string language, CancellationToken cancellationToken)
    {
        var query = Scope(_db.InventoryCheckDocuments.AsNoTracking().Include(x => x.Warehouse).Include(x => x.Lines).AsQueryable(), x => x.WarehouseId);
        query = ApplyDocumentFilter(query, keyword, fromDate, toDate);
        return await query.OrderByDescending(x => x.DocumentDate).ThenByDescending(x => x.PostedAt).Take(100)
            .Select(x => new { id = x.Id, documentNo = x.DocumentNo, documentDate = x.DocumentDate, party = x.ResponsibleStaff, warehouse = x.Warehouse != null ? x.Warehouse.WarehouseCode : "", status = LocalizationCatalog.Text(language, x.SessionStatus), sessionStatus = x.SessionStatus, lines = x.Lines.Count, createdBy = x.CreatedBy, approvedBy = x.ApprovedBy, postedAt = x.PostedAt })
            .ToArrayAsync(cancellationToken);
    }

    private async Task<object[]> RepairRows(string? keyword, DateTime? fromDate, DateTime? toDate, string language, CancellationToken cancellationToken)
    {
        var allowedBinIds = await AllowedBinIds(cancellationToken);
        var query = _db.RepairDocuments.AsNoTracking().Include(x => x.RepairVendor).Include(x => x.Lines).AsQueryable();
        if (allowedBinIds != null)
        {
            query = query.Where(x => x.Lines.Any(l =>
                (l.FromBinLocationId.HasValue && allowedBinIds.Contains(l.FromBinLocationId.Value)) ||
                (l.TargetBinLocationId.HasValue && allowedBinIds.Contains(l.TargetBinLocationId.Value))));
        }

        query = ApplyDocumentFilter(query, keyword, fromDate, toDate);
        return await query.OrderByDescending(x => x.DocumentDate).Take(100)
            .Select(x => new { id = x.Id, documentNo = x.DocumentNo, documentDate = x.DocumentDate, party = x.RepairVendor != null ? x.RepairVendor.Name : "", warehouse = "", status = x.Lines.Any(l => !l.IsReturned) ? "Repairing" : "Finalized", lines = x.Lines.Count, createdBy = x.CreatedBy, approvedBy = x.ApprovedBy, postedAt = x.PostedAt })
            .ToArrayAsync(cancellationToken);
    }

    private async Task<object[]> BorrowRows(string? keyword, DateTime? fromDate, DateTime? toDate, string language, CancellationToken cancellationToken)
    {
        var allowedBinIds = await AllowedBinIds(cancellationToken);
        var query = _db.BorrowDocuments.AsNoTracking().Include(x => x.Borrower).Include(x => x.Lines).AsQueryable();
        if (allowedBinIds != null)
        {
            query = query.Where(x => x.Lines.Any(l =>
                (l.FromBinLocationId.HasValue && allowedBinIds.Contains(l.FromBinLocationId.Value)) ||
                (l.TargetBinLocationId.HasValue && allowedBinIds.Contains(l.TargetBinLocationId.Value))));
        }

        query = ApplyDocumentFilter(query, keyword, fromDate, toDate);
        return await query.OrderByDescending(x => x.DocumentDate).Take(100)
            .Select(x => new { id = x.Id, documentNo = x.DocumentNo, documentDate = x.DocumentDate, party = x.Borrower != null ? x.Borrower.Name : "", warehouse = "", status = x.Lines.Any(l => !l.IsReturned) ? "Borrow" : "Returned", lines = x.Lines.Count, createdBy = x.CreatedBy, approvedBy = x.ApprovedBy, postedAt = x.PostedAt })
            .ToArrayAsync(cancellationToken);
    }

    // ─── Detail queries ──────────────────────────────────────

    private async Task<object?> InboundDetail(int id, string language, CancellationToken cancellationToken)
    {
        var doc = await Scope(_db.InboundDocuments.AsNoTracking()
            .Include(x => x.Warehouse).Include(x => x.SourceExternalParty)
            .Include(x => x.Lines).ThenInclude(x => x.Item)
            .Include(x => x.Lines).ThenInclude(x => x.BinLocation)
            .AsQueryable(), x => x.WarehouseId)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (doc == null) return null;

        var logs = await _db.InboundDocumentLogs.AsNoTracking()
            .Where(x => x.InboundDocumentId == id)
            .OrderBy(x => x.Timestamp)
            .Select(x => new {
                x.ItemInstanceId,
                x.Timestamp,
                ActionType = x.Action,
                x.OldStatus,
                x.NewStatus,
                x.Receiver,
                x.ReceiverPhone,
                x.ReceiverDepartment,
                x.DepartmentOwner,
                x.OldLocationText,
                x.NewLocationText,
                x.PerformedBy,
                x.Note,
                ItemCode = x.ItemInstance != null && x.ItemInstance.Item != null ? x.ItemInstance.Item.ItemCode : null,
                SerialNumber = x.ItemInstance != null ? x.ItemInstance.SerialNumber : null
            })
            .ToListAsync(cancellationToken);

        var history = logs.OrderByDescending(x => x.Timestamp).Select(x => new {
            timestamp = x.Timestamp,
            actionType = x.ActionType,
            actionTypeText = LocalizationCatalog.Text(language, x.ActionType == "InboundReceive" ? "Inbound" : x.ActionType),
            itemCode = x.ItemCode,
            serialNumber = x.SerialNumber,
            oldStatus = x.OldStatus,
            oldStatusText = LocalizationCatalog.Text(language, x.OldStatus),
            newStatus = x.NewStatus,
            newStatusText = LocalizationCatalog.Text(language, x.NewStatus),
            receiver = x.Receiver,
            receiverPhone = x.ReceiverPhone,
            receiverDepartment = x.ReceiverDepartment,
            departmentOwner = x.DepartmentOwner,
            oldLocation = x.OldLocationText,
            newLocation = x.NewLocationText,
            performedBy = x.PerformedBy
        }).ToList();

        return new
        {
            header = Header(doc, language, doc.Warehouse?.WarehouseCode, doc.SourceExternalParty?.Name),
            lines = doc.Lines.Select(x => new { item = x.Item?.ItemCode, serial = x.SerialNumber, barcode = x.Barcode, bin = x.BinLocation?.BinCode, condition = x.Condition, note = x.Note }),
            history
        };
    }

    private async Task<object?> MoveDetail(int id, string language, CancellationToken cancellationToken)
    {
        var doc = await Scope(_db.MoveDocuments.AsNoTracking()
            .Include(x => x.Warehouse)
            .Include(x => x.Lines).ThenInclude(x => x.ItemInstance)!.ThenInclude(x => x!.Item)
            .Include(x => x.Lines).ThenInclude(x => x.FromBinLocation)
            .Include(x => x.Lines).ThenInclude(x => x.TargetBinLocation)
            .AsQueryable(), x => x.WarehouseId)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return doc == null ? null : new
        {
            header = Header(doc, language, doc.Warehouse?.WarehouseCode, null),
            lines = doc.Lines.Select(x => new { item = x.ItemInstance?.Item?.ItemCode, serial = x.ItemInstance?.SerialNumber, from = x.FromBinLocation?.BinCode, to = x.TargetBinLocation?.BinCode, note = x.Note, status = x.ItemInstance?.Status })
        };
    }

    private async Task<object?> AdjustmentDetail(int id, string language, CancellationToken cancellationToken)
    {
        // Pre-load bins to avoid N+1 subquery inside LINQ projection
        var doc = await Scope(_db.AdjustmentDocuments.AsNoTracking()
            .Include(x => x.Warehouse)
            .Include(x => x.Lines).ThenInclude(x => x.ItemInstance)!.ThenInclude(x => x!.Item)
            .AsQueryable(), x => x.WarehouseId)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (doc == null) return null;

        var binIds = doc.Lines.Where(x => x.TargetBinLocationId.HasValue).Select(x => x.TargetBinLocationId!.Value).Distinct().ToArray();
        var binMap = binIds.Length > 0
            ? await _db.BinLocations.AsNoTracking().Where(b => binIds.Contains(b.Id) && b.IsActive).ToDictionaryAsync(b => b.Id, b => b.BinCode, cancellationToken)
            : new Dictionary<int, string>();
        var unknownText = LocalizationCatalog.Text(language, "Unknown");

        var logs = await _db.AdjustmentDocumentLogs.AsNoTracking()
            .Where(x => x.AdjustmentDocumentId == id)
            .OrderBy(x => x.Timestamp)
            .Select(x => new {
                x.ItemInstanceId,
                x.Timestamp,
                ActionType = x.Action,
                x.OldStatus,
                x.NewStatus,
                x.OldLocationText,
                x.NewLocationText,
                x.Reason,
                x.PerformedBy,
                x.Note,
                ItemCode = x.ItemInstance != null && x.ItemInstance.Item != null ? x.ItemInstance.Item.ItemCode : null,
                SerialNumber = x.ItemInstance != null ? x.ItemInstance.SerialNumber : null
            })
            .ToListAsync(cancellationToken);

        var history = logs.OrderByDescending(x => x.Timestamp).Select(x => new {
            timestamp = x.Timestamp,
            actionType = x.ActionType,
            actionTypeText = LocalizationCatalog.Text(language, x.ActionType),
            itemCode = x.ItemCode,
            serialNumber = x.SerialNumber,
            oldStatus = x.OldStatus,
            oldStatusText = LocalizationCatalog.Text(language, x.OldStatus),
            newStatus = x.NewStatus,
            newStatusText = LocalizationCatalog.Text(language, x.NewStatus),
            oldLocation = x.OldLocationText,
            newLocation = x.NewLocationText,
            reason = x.Reason,
            performedBy = x.PerformedBy
        }).ToList();

        return new
        {
            header = Header(doc, language, doc.Warehouse?.WarehouseCode, doc.Reason),
            lines = doc.Lines.Select(x => new
            {
                item = x.ItemInstance?.Item?.ItemCode,
                serial = x.ItemInstance?.SerialNumber,
                oldStatus = x.OldStatus,
                newStatus = x.NewStatus,
                reason = x.Reason,
                bin = x.TargetBinLocationId.HasValue && binMap.TryGetValue(x.TargetBinLocationId.Value, out var path) ? path : unknownText,
                note = x.Reason
            }),
            history
        };
    }

    private async Task<object?> InventoryCheckDetail(int id, string language, CancellationToken cancellationToken)
    {
        // Pre-load bins to avoid N+1 subquery inside LINQ projection
        var doc = await Scope(_db.InventoryCheckDocuments.AsNoTracking()
            .Include(x => x.Warehouse)
            .Include(x => x.Lines).ThenInclude(x => x.ItemInstance)!.ThenInclude(x => x!.Item)
            .AsQueryable(), x => x.WarehouseId)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (doc == null) return null;

        var binIds = doc.Lines.Where(x => x.ActualBinLocationId.HasValue).Select(x => x.ActualBinLocationId!.Value).Distinct().ToArray();
        var binMap = binIds.Length > 0
            ? await _db.BinLocations.AsNoTracking().Where(b => binIds.Contains(b.Id) && b.IsActive).ToDictionaryAsync(b => b.Id, b => b.BinCode, cancellationToken)
            : new Dictionary<int, string>();
        var unknownText = LocalizationCatalog.Text(language, "Unknown");

        return new
        {
            header = Header(doc, language, doc.Warehouse?.WarehouseCode, doc.ResponsibleStaff,
                new { doc.SessionStatus, doc.CountMethod }),
            lines = doc.Lines.Select(x => new
            {
                item = x.ItemInstance?.Item?.ItemCode,
                serial = x.ItemInstance?.SerialNumber,
                result = x.Result,
                note = x.Note,
                bin = x.ActualBinLocationId.HasValue && binMap.TryGetValue(x.ActualBinLocationId.Value, out var path) ? path : unknownText
            })
        };
    }

    private async Task<object?> RepairDetail(int id, string language, CancellationToken cancellationToken)
    {
        var allowedBinIds = await AllowedBinIds(cancellationToken);
        var doc = await _db.RepairDocuments.AsNoTracking()
            .Include(x => x.RepairVendor)
            .Include(x => x.Lines).ThenInclude(x => x.ItemInstance)!.ThenInclude(x => x!.Item)
            .Include(x => x.Lines).ThenInclude(x => x.FromBinLocation)
            .Include(x => x.Lines).ThenInclude(x => x.TargetBinLocation)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (doc == null || (allowedBinIds != null && !doc.Lines.Any(x =>
            (x.FromBinLocationId.HasValue && allowedBinIds.Contains(x.FromBinLocationId.Value)) ||
            (x.TargetBinLocationId.HasValue && allowedBinIds.Contains(x.TargetBinLocationId.Value))))) return null;

        var logs = await _db.RepairDocumentLogs.AsNoTracking()
            .Where(x => x.RepairDocumentId == id)
            .OrderBy(x => x.Timestamp)
            .Select(x => new {
                x.ItemInstanceId,
                x.Timestamp,
                ActionType = x.Action,
                x.OldStatus,
                x.NewStatus,
                x.RepairVendorName,
                x.ExternalLocation,
                x.OldLocationText,
                x.NewLocationText,
                x.RepairResultNote,
                x.PerformedBy,
                x.Note,
                ItemCode = x.ItemInstance != null && x.ItemInstance.Item != null ? x.ItemInstance.Item.ItemCode : null,
                SerialNumber = x.ItemInstance != null ? x.ItemInstance.SerialNumber : null
            })
            .ToListAsync(cancellationToken);

        var history = logs.OrderByDescending(x => x.Timestamp).Select(x => new {
            timestamp = x.Timestamp,
            actionType = x.ActionType,
            actionTypeText = LocalizationCatalog.Text(language, x.ActionType),
            itemCode = x.ItemCode,
            serialNumber = x.SerialNumber,
            oldStatus = x.OldStatus,
            oldStatusText = LocalizationCatalog.Text(language, x.OldStatus),
            newStatus = x.NewStatus,
            newStatusText = LocalizationCatalog.Text(language, x.NewStatus),
            repairVendor = x.RepairVendorName,
            externalLocation = x.ExternalLocation,
            oldLocation = x.OldLocationText,
            newLocation = x.NewLocationText,
            repairResultNote = x.RepairResultNote,
            performedBy = x.PerformedBy
        }).ToList();

        return new
        {
            header = Header(doc, language, null, doc.RepairVendor?.Name),
            lines = doc.Lines.Select(x => new { item = x.ItemInstance?.Item?.ItemCode, serial = x.ItemInstance?.SerialNumber, fromBin = x.FromBinLocation?.BinCode, targetBin = x.TargetBinLocation?.BinCode ?? x.TargetExternalLocation, status = x.ItemInstance!.Status, newSerial = x.NewSerialNumber, note = x.RepairResultNote }),
            history
        };
    }

    private async Task<object?> BorrowDetail(int id, string language, CancellationToken cancellationToken)
    {
        var allowedBinIds = await AllowedBinIds(cancellationToken);
        var doc = await _db.BorrowDocuments.AsNoTracking()
            .Include(x => x.Borrower)
            .Include(x => x.Lines).ThenInclude(x => x.ItemInstance)!.ThenInclude(x => x!.Item)
            .Include(x => x.Lines).ThenInclude(x => x.FromBinLocation)
            .Include(x => x.Lines).ThenInclude(x => x.TargetBinLocation)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (doc == null || (allowedBinIds != null && !doc.Lines.Any(x =>
            (x.FromBinLocationId.HasValue && allowedBinIds.Contains(x.FromBinLocationId.Value)) ||
            (x.TargetBinLocationId.HasValue && allowedBinIds.Contains(x.TargetBinLocationId.Value))))) return null;

        var logs = await _db.BorrowDocumentLogs.AsNoTracking()
            .Where(x => x.BorrowDocumentId == id)
            .OrderBy(x => x.Timestamp)
            .Select(x => new {
                x.ItemInstanceId,
                x.Timestamp,
                ActionType = x.Action,
                x.OldStatus,
                x.NewStatus,
                x.Borrower,
                x.BorrowDepartment,
                x.BorrowerPhone,
                x.DepartmentOwner,
                x.OldLocationText,
                x.NewLocationText,
                x.PerformedBy,
                x.Note,
                ItemCode = x.ItemInstance != null && x.ItemInstance.Item != null ? x.ItemInstance.Item.ItemCode : null,
                SerialNumber = x.ItemInstance != null ? x.ItemInstance.SerialNumber : null
            })
            .ToListAsync(cancellationToken);

        var history = logs.OrderByDescending(x => x.Timestamp).Select(x => new {
            timestamp = x.Timestamp,
            actionType = x.ActionType,
            actionTypeText = LocalizationCatalog.Text(language, x.ActionType),
            itemCode = x.ItemCode,
            serialNumber = x.SerialNumber,
            oldStatus = x.OldStatus,
            oldStatusText = LocalizationCatalog.Text(language, x.OldStatus),
            newStatus = x.NewStatus,
            newStatusText = LocalizationCatalog.Text(language, x.NewStatus),
            borrower = x.Borrower,
            borrowDepartment = x.BorrowDepartment,
            borrowerPhone = x.BorrowerPhone,
            departmentOwner = x.DepartmentOwner,
            oldLocation = x.OldLocationText,
            newLocation = x.NewLocationText,
            performedBy = x.PerformedBy
        }).ToList();

        return new
        {
            header = Header(doc, language, null, doc.Borrower?.Name, new { doc.Purpose, doc.BorrowDepartment, doc.BorrowerPhone, doc.DepartmentOwner, doc.DueDate }),
            lines = doc.Lines.Select(x => new { item = x.ItemInstance?.Item?.ItemCode, serial = x.ItemInstance?.SerialNumber, fromBin = x.FromBinLocation?.BinCode, targetBin = x.TargetBinLocation?.BinCode ?? x.TargetExternalLocation, returned = x.IsReturned, condition = x.IsReturned ? "Returned" : "LentOut", returnedAt = x.ReturnedAt, note = x.Note }),
            history
        };
    }

    // ─── Shared helpers ──────────────────────────────────────

    private object Header(ERP.Inventory.Domain.Common.DocumentBase doc, string language, string? warehouse, string? party, object? extra = null)
    {
        return new { doc.Id, EntityName = doc.GetType().Name, doc.DocumentNo, doc.DocumentDate, warehouse, party, status = LocalizationCatalog.EnumText(language, doc.Status), doc.CreatedBy, doc.ApprovedBy, doc.ApprovedAt, doc.PostedAt, doc.Note, extra };
    }

    private IQueryable<T> Scope<T>(IQueryable<T> query, System.Linq.Expressions.Expression<Func<T, int>> warehouseSelector)
    {
        var user = _currentUserService.GetCurrentUser();
        return user.IsAdmin ? query : query.Where(BuildWarehousePredicate(warehouseSelector, user.WarehouseIds));
    }

    private static System.Linq.Expressions.Expression<Func<T, bool>> BuildWarehousePredicate<T>(System.Linq.Expressions.Expression<Func<T, int>> warehouseSelector, IReadOnlyCollection<int> warehouseIds)
    {
        var body = System.Linq.Expressions.Expression.Call(typeof(Enumerable), nameof(Enumerable.Contains), new[] { typeof(int) }, System.Linq.Expressions.Expression.Constant(warehouseIds), warehouseSelector.Body);
        return System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(body, warehouseSelector.Parameters);
    }

    private static IQueryable<T> ApplyDocumentFilter<T>(IQueryable<T> query, string? keyword, DateTime? fromDate, DateTime? toDate) where T : ERP.Inventory.Domain.Common.DocumentBase
    {
        if (fromDate.HasValue) query = query.Where(x => x.DocumentDate >= fromDate.Value);
        if (toDate.HasValue)
        {
            var to = toDate.Value.Date.AddDays(1);
            query = query.Where(x => x.DocumentDate < to);
        }
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var key = keyword.Trim();
            query = query.Where(x => x.DocumentNo.Contains(key) || (x.Note != null && x.Note.Contains(key)) || x.CreatedBy.Contains(key) || x.ApprovedBy.Contains(key));
        }
        return query;
    }

    private async Task<int[]?> AllowedBinIds(CancellationToken cancellationToken)
    {
        var user = _currentUserService.GetCurrentUser();
        if (user.IsAdmin) return null;
        return await _db.BinLocations.AsNoTracking()
            .Where(x => user.WarehouseIds.Contains(x.WarehouseId))
            .Select(x => x.Id)
            .ToArrayAsync(cancellationToken);
    }

    private string Language() => User.FindFirst("language")?.Value ?? "vi";
}
