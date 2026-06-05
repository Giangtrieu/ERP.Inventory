using ERP.Inventory.Application.Interfaces;
using ERP.Inventory.Domain.Entities;
using ERP.Inventory.Infrastructure.Data;
using ERP.Inventory.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ERP.Inventory.Web.Controllers;

[Authorize]
[Route("[controller]")]
public sealed class DocumentsController : Controller
{
    private readonly InventoryDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDocumentLifecycleService _documentLifecycleService;

    public DocumentsController(InventoryDbContext db, ICurrentUserService currentUserService, IDocumentLifecycleService documentLifecycleService)
    {
        _db = db;
        _currentUserService = currentUserService;
        _documentLifecycleService = documentLifecycleService;
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
            "quantity-receive" or "quantity-issue" or "quantity-adjust" => await QuantityRows(type, keyword, fromDate, toDate, language, cancellationToken),
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
            "quantity-receive" or "quantity-issue" or "quantity-adjust" => await QuantityDetail(id, language, cancellationToken),
            "inventory-check" => await InventoryCheckDetail(id, language, cancellationToken),
            "repair-send" or "repair-receive" => await RepairDetail(id, language, cancellationToken),
            "borrow-lend" or "borrow-return" => await BorrowDetail(id, language, cancellationToken),
            _ => null
        };

        return detail == null ? NotFound() : Json(detail);
    }

    [HttpGet("PdfData")]
    public async Task<IActionResult> PdfData([FromQuery] string type, [FromQuery] int id, [FromQuery] string? action, [FromQuery] DateTime? printDate, CancellationToken cancellationToken)
    {
        var language = Language();
        var detail = type switch
        {
            "inbound" => await InboundDetail(id, language, cancellationToken),
            "move" => await MoveDetail(id, language, cancellationToken),
            "adjustment" => await AdjustmentDetail(id, language, cancellationToken),
            "quantity-receive" or "quantity-issue" or "quantity-adjust" => await QuantityDetail(id, language, cancellationToken),
            "inventory-check" => await InventoryCheckDetail(id, language, cancellationToken),
            "repair-send" or "repair-receive" => await RepairDetail(id, language, cancellationToken),
            "borrow-lend" or "borrow-return" => await BorrowDetail(id, language, cancellationToken),
            _ => null
        };

        if (detail == null)
        {
            return NotFound(new { success = false, errorType = "NotFound", message = "Document not found." });
        }

        var data = BuildPdfData(type, detail, language, action, printDate ?? DateTime.UtcNow);
        return Json(new { success = true, data });
    }

    [HttpPost("Delete")]
    [Authorize(Roles = "Admin,Warehouse Manager")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete([FromQuery] string type, [FromQuery] int id, CancellationToken cancellationToken)
    {
        var result = await _documentLifecycleService.DeleteAsync(type, id, _currentUserService.GetCurrentUser(), cancellationToken);
        return Json(result);
    }

    [HttpPost("Edit")]
    [Authorize(Roles = "Admin,Warehouse Manager")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit([FromQuery] string type, [FromQuery] int id, [FromBody] JsonElement payload, CancellationToken cancellationToken)
    {
        var result = await _documentLifecycleService.EditAsync(type, id, payload, _currentUserService.GetCurrentUser(), cancellationToken);
        return Json(result);
    }

    [HttpPost("Rebuild")]
    [Authorize(Roles = "Admin,Warehouse Manager")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rebuild([FromQuery] string type, [FromQuery] int id, CancellationToken cancellationToken)
    {
        var result = await _documentLifecycleService.RebuildAsync(type, id, _currentUserService.GetCurrentUser(), cancellationToken);
        return Json(result);
    }

    [HttpGet("EditModel")]
    public async Task<IActionResult> EditModel([FromQuery] string type, [FromQuery] int id, CancellationToken cancellationToken)
    {
        var result = await _documentLifecycleService.GetEditModelAsync(type, id, _currentUserService.GetCurrentUser(), cancellationToken);
        return Json(result);
    }

    [HttpGet("Dependencies")]
    public async Task<IActionResult> Dependencies([FromQuery] string type, [FromQuery] int id, [FromQuery] string action = "Delete", CancellationToken cancellationToken = default)
    {
        var result = await _documentLifecycleService.PreviewDependenciesAsync(type, id, action, _currentUserService.GetCurrentUser(), cancellationToken);
        return Json(result);
    }

    // ─── List queries ────────────────────────────────────────

    private async Task<object[]> InboundRows(string? keyword, DateTime? fromDate, DateTime? toDate, string language, CancellationToken cancellationToken)
    {
        var query = Scope(_db.InboundDocuments.AsNoTracking().Include(x => x.Warehouse).Include(x => x.SourceExternalParty).Include(x => x.Lines).AsQueryable(), x => x.WarehouseId);
        query = ApplyDocumentFilter(query, keyword, fromDate, toDate);
        return await query.OrderByDescending(x => x.Id).Take(100)
            .Select(x => new { id = x.Id, documentNo = x.DocumentNo, documentDate = x.DocumentDate, party = x.SourceExternalParty != null ? x.SourceExternalParty.Name : "", warehouse = x.Warehouse != null ? x.Warehouse.WarehouseCode : "", status = LocalizationCatalog.EnumText(language, x.Status), lines = x.Lines.Count(l => l.ItemInstance == null || !l.ItemInstance.IsDeleted), createdBy = x.CreatedBy, approvedBy = x.ApprovedBy, postedAt = x.PostedAt })
            .ToArrayAsync(cancellationToken);
    }

    private async Task<object[]> MoveRows(string? keyword, DateTime? fromDate, DateTime? toDate, string language, CancellationToken cancellationToken)
    {
        var query = Scope(_db.MoveDocuments.AsNoTracking().Include(x => x.Warehouse).Include(x => x.Lines).AsQueryable(), x => x.WarehouseId);
        query = ApplyDocumentFilter(query, keyword, fromDate, toDate);
        return await query.OrderByDescending(x => x.Id).Take(100)
            .Select(x => new { id = x.Id, documentNo = x.DocumentNo, documentDate = x.DocumentDate, party = "", warehouse = x.Warehouse != null ? x.Warehouse.WarehouseCode : "", status = LocalizationCatalog.EnumText(language, x.Status), lines = x.Lines.Count(l => l.ItemInstance == null || !l.ItemInstance.IsDeleted), createdBy = x.CreatedBy, approvedBy = x.ApprovedBy, postedAt = x.PostedAt })
            .ToArrayAsync(cancellationToken);
    }

    private async Task<object[]> AdjustmentRows(string? keyword, DateTime? fromDate, DateTime? toDate, string language, CancellationToken cancellationToken)
    {
        var query = Scope(_db.AdjustmentDocuments.AsNoTracking().Include(x => x.Warehouse).Include(x => x.Lines).AsQueryable(), x => x.WarehouseId);
        query = ApplyDocumentFilter(query, keyword, fromDate, toDate);
        return await query.OrderByDescending(x => x.Id).Take(100)
            .Select(x => new { id = x.Id, documentNo = x.DocumentNo, documentDate = x.DocumentDate, party = x.Reason, warehouse = x.Warehouse != null ? x.Warehouse.WarehouseCode : "", status = LocalizationCatalog.EnumText(language, x.Status), lines = x.Lines.Count(l => l.ItemInstance == null || !l.ItemInstance.IsDeleted), createdBy = x.CreatedBy, approvedBy = x.ApprovedBy, postedAt = x.PostedAt })
            .ToArrayAsync(cancellationToken);
    }

    private async Task<object[]> QuantityRows(string type, string? keyword, DateTime? fromDate, DateTime? toDate, string language, CancellationToken cancellationToken)
    {
        var docType = type switch
        {
            "quantity-receive" => ERP.Inventory.Domain.Enums.QuantityInventoryDocumentType.Receive,
            "quantity-issue" => ERP.Inventory.Domain.Enums.QuantityInventoryDocumentType.Issue,
            _ => ERP.Inventory.Domain.Enums.QuantityInventoryDocumentType.Adjust
        };

        var query = Scope(_db.QuantityInventoryDocuments.AsNoTracking().Include(x => x.Warehouse).Include(x => x.Lines).AsQueryable(), x => x.WarehouseId)
            .Where(x => x.DocumentType == docType);

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

        return await query.OrderByDescending(x => x.Id).Take(100)
            .Select(x => new
            {
                id = x.Id,
                documentNo = x.DocumentNo,
                documentDate = x.DocumentDate,
                party = x.DocumentType.ToString(),
                warehouse = x.Warehouse != null ? x.Warehouse.WarehouseCode : "",
                status = LocalizationCatalog.Text(language, $"Enum.QuantityInventoryDocumentType.{x.DocumentType}"),
                lines = x.Lines.Count,
                createdBy = x.CreatedBy,
                approvedBy = x.ApprovedBy,
                postedAt = x.PostedAt
            })
            .ToArrayAsync(cancellationToken);
    }

    private async Task<object[]> InventoryCheckRows(string? keyword, DateTime? fromDate, DateTime? toDate, string language, CancellationToken cancellationToken)
    {
        var query = Scope(_db.InventoryCheckDocuments.AsNoTracking().Include(x => x.Warehouse).Include(x => x.Lines).AsQueryable(), x => x.WarehouseId);
        query = ApplyDocumentFilter(query, keyword, fromDate, toDate);
        return await query.OrderByDescending(x =>  x.Id).Take(100)
            .Select(x => new { id = x.Id, documentNo = x.DocumentNo, documentDate = x.DocumentDate, party = x.ResponsibleStaff, warehouse = x.Warehouse != null ? x.Warehouse.WarehouseCode : "", status = LocalizationCatalog.Text(language, x.SessionStatus), sessionStatus = x.SessionStatus, lines = x.Lines.Count(l => l.ItemInstance == null || !l.ItemInstance.IsDeleted), createdBy = x.CreatedBy, approvedBy = x.ApprovedBy, postedAt = x.PostedAt })
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
        return await query.OrderByDescending(x => x.Id).Take(100)
            .Select(x => new { id = x.Id, documentNo = x.DocumentNo, documentDate = x.DocumentDate, party = x.RepairVendor != null ? x.RepairVendor.Name : "", warehouse = "", status = x.Lines.Any(l => !l.IsReturned && (l.ItemInstance == null || !l.ItemInstance.IsDeleted)) ? "Repairing" : "Finalized", lines = x.Lines.Count(l => l.ItemInstance == null || !l.ItemInstance.IsDeleted), createdBy = x.CreatedBy, approvedBy = x.ApprovedBy, postedAt = x.PostedAt })
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
        return await query.OrderByDescending(x => x.Id).Take(100)
            .Select(x => new { id = x.Id, documentNo = x.DocumentNo, documentDate = x.DocumentDate, party = x.Borrower != null ? x.Borrower.Name : "", warehouse = "", status = x.Lines.Any(l => !l.IsReturned && (l.ItemInstance == null || !l.ItemInstance.IsDeleted)) ? "Borrow" : "Returned", lines = x.Lines.Count(l => l.ItemInstance == null || !l.ItemInstance.IsDeleted), createdBy = x.CreatedBy, approvedBy = x.ApprovedBy, postedAt = x.PostedAt })
            .ToArrayAsync(cancellationToken);
    }

    // ─── Detail queries ──────────────────────────────────────

    private async Task<object?> InboundDetail(int id, string language, CancellationToken cancellationToken)
    {
        var doc = await Scope(_db.InboundDocuments.AsNoTracking()
            .Include(x => x.Warehouse).Include(x => x.SourceExternalParty)
            //.Include(x => x.Lines).ThenInclude(x => x.Item)
            .Include(x => x.Lines).ThenInclude(x => x.ItemInstance).ThenInclude(x => x.Item)
            .Include(x => x.Lines).ThenInclude(x => x.BinLocation)
            .AsQueryable(), x => x.WarehouseId)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (doc == null) return null;

        var logs = await _db.InboundDocumentLogs.AsNoTracking()
            .Where(x => x.InboundDocumentId == id)
            .Where(x => x.ItemInstance == null || !x.ItemInstance.IsDeleted)
            .OrderByDescending(x => x.Id)
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
            lines = doc.Lines.Where(x => x.ItemInstance == null || !x.ItemInstance.IsDeleted).Select(x => new { item = x.ItemInstance?.Item?.ItemCode, serial = x.ItemInstance?.SerialNumber, barcode = x.Barcode, ownerName = x.ItemInstance != null ? x.ItemInstance.OwnerName : null, bin = x.BinLocation?.BinCode, condition = x.Condition, note = x.Note }),
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
            lines = doc.Lines.Where(x => x.ItemInstance == null || !x.ItemInstance.IsDeleted).Select(x => new { item = x.ItemInstance?.Item?.ItemCode, serial = x.ItemInstance?.SerialNumber, ownerName = x.ItemInstance != null ? x.ItemInstance.OwnerName : null, from = x.FromBinLocation?.BinCode, to = x.TargetBinLocation?.BinCode, note = x.Note, status = x.ItemInstance?.Status })
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
            .Where(x => x.ItemInstance == null || !x.ItemInstance.IsDeleted)
            .OrderByDescending(x => x.Id)
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
            lines = doc.Lines.Where(x => x.ItemInstance == null || !x.ItemInstance.IsDeleted).Select(x => new
            {
                item = x.ItemInstance?.Item?.ItemCode,
                serial = x.ItemInstance?.SerialNumber,
                ownerName = x.ItemInstance != null ? x.ItemInstance.OwnerName : null,
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
            lines = doc.Lines.Where(x => x.ItemInstance == null || !x.ItemInstance.IsDeleted).Select(x => new
            {
                item = x.ItemInstance?.Item?.ItemCode,
                serial = x.ItemInstance?.SerialNumber,
                ownerName = x.ItemInstance != null ? x.ItemInstance.OwnerName : null,
                result = x.Result,
                note = x.Note,
                bin = x.ActualBinLocationId.HasValue && binMap.TryGetValue(x.ActualBinLocationId.Value, out var path) ? path : unknownText
            })
        };
    }

    private async Task<object?> QuantityDetail(int id, string language, CancellationToken cancellationToken)
    {
        var doc = await Scope(_db.QuantityInventoryDocuments.AsNoTracking()
            .Include(x => x.Warehouse)
            .Include(x => x.Lines).ThenInclude(x => x.Item)!.ThenInclude(x => x.Category)
            .Include(x => x.Lines).ThenInclude(x => x.BinLocation)
            .AsQueryable(), x => x.WarehouseId)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (doc == null) return null;

        var history = await _db.QuantityInventoryTransactions.AsNoTracking()
            .Where(x => x.DocumentId == id)
            .OrderByDescending(x => x.Id)
            .Select(x => new
            {
                timestamp = x.PostedAt,
                actionType = x.TransactionType.ToString(),
                actionTypeText = LocalizationCatalog.Text(language, $"Enum.QuantityInventoryDocumentType.{x.TransactionType}"),
                itemCode = x.Item != null ? x.Item.ItemCode : null,
                snCode = x.SnCode,
                status = x.StatusAfter.ToString(),
                statusText = LocalizationCatalog.Text(language, x.StatusAfter.ToString()),
                quantityDelta = x.QuantityDelta,
                binLocationId = x.BinLocationId,
                binCode = x.BinCode,
                receiver =string.IsNullOrWhiteSpace(x.ReceiverCode) && string.IsNullOrWhiteSpace(x.ReceiverName) ? null
                            : $"{x.ReceiverCode}-{x.ReceiverName}",

                sender =string.IsNullOrWhiteSpace(x.SenderCode) && string.IsNullOrWhiteSpace(x.SenderName) ? null
                            : $"{x.SenderCode}-{x.SenderName}",
                department = "TE",
                oldLocation = !string.IsNullOrWhiteSpace(x.BinCode) ? x.BinCode : (x.QuantityDelta > 0 ? x.Warehouse.Name : ""),
                receiverPhone = string.IsNullOrWhiteSpace(x.ReceiverPhone)  ? x.SenderPhone : x.ReceiverPhone,
                performedBy = x.PostedBy,
                itemCategory = x.Item != null ? x.Item.Category.CategoryCode : null,
            })
            .ToListAsync(cancellationToken);

        return new
        {
            header = new
            {
                doc.Id,
                EntityName = nameof(QuantityInventoryDocument),
                doc.DocumentNo,
                doc.DocumentDate,
                warehouse = doc.Warehouse?.WarehouseCode,
                party = "",
                status = LocalizationCatalog.Text(language, $"Enum.QuantityInventoryDocumentType.{doc.DocumentType}"),
                doc.CreatedBy,
                doc.ApprovedBy,
                approvedAt = doc.CreatedAt,
                doc.PostedAt,
                doc.Note
            },
            lines = doc.Lines.Select(x => new { itemCategory = x.Item?.Category?.CategoryCode, item = x.Item?.ItemCode, quantity = x.Quantity, note = x.Note, binLocationId = x.BinLocationId, binCode = x.BinLocation?.BinCode ?? string.Empty, location = x.BinLocation?.BinCode ?? string.Empty }),
            history
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
            (x.ItemInstance == null || !x.ItemInstance.IsDeleted) &&
            ((x.FromBinLocationId.HasValue && allowedBinIds.Contains(x.FromBinLocationId.Value)) ||
             (x.TargetBinLocationId.HasValue && allowedBinIds.Contains(x.TargetBinLocationId.Value)))))) return null;

        var logs = await _db.RepairDocumentLogs.AsNoTracking()
            .Where(x => x.RepairDocumentId == id)
            .Where(x => x.ItemInstance == null || !x.ItemInstance.IsDeleted)
            .OrderByDescending(x => x.Id)
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
            lines = doc.Lines.Where(x => x.ItemInstance == null || !x.ItemInstance.IsDeleted).Select(x => new { item = x.ItemInstance?.Item?.ItemCode, serial = x.ItemInstance?.SerialNumber, ownerName = x.ItemInstance != null ? x.ItemInstance.OwnerName : null, fromBin = x.FromBinLocation?.BinCode, targetBin = x.TargetBinLocation?.BinCode ?? x.TargetExternalLocation, status = x.ItemInstance?.Status, newSerial = x.NewSerialNumber, note = x.RepairResultNote }),
            history
        };
    }

    private async Task<object?> BorrowDetail(int id, string language, CancellationToken cancellationToken)
    {
        var allowedBinIds = await AllowedBinIds(cancellationToken);
        var doc = await _db.BorrowDocuments.AsNoTracking()
            .Include(x => x.Borrower)
            .Include(x => x.Lines).ThenInclude(x => x.ItemInstance)!.ThenInclude(x => x!.Item)!.ThenInclude(x => x!.Category)
            .Include(x => x.Lines).ThenInclude(x => x.FromBinLocation)
            .Include(x => x.Lines).ThenInclude(x => x.TargetBinLocation)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (doc == null || (allowedBinIds != null && !doc.Lines.Any(x =>
            (x.ItemInstance == null || !x.ItemInstance.IsDeleted) &&
            ((x.FromBinLocationId.HasValue && allowedBinIds.Contains(x.FromBinLocationId.Value)) ||
             (x.TargetBinLocationId.HasValue && allowedBinIds.Contains(x.TargetBinLocationId.Value)))))) return null;

        var logs = await _db.BorrowDocumentLogs.AsNoTracking()
            .Where(x => x.BorrowDocumentId == id)
            .Where(x => x.ItemInstance == null || !x.ItemInstance.IsDeleted)
            .OrderByDescending(x => x.Id)
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
            header = Header(doc, language, null, doc.Borrower?.Name, new { doc.Purpose, doc.BorrowDepartment, doc.BorrowerPhone, doc.DepartmentOwner, doc.DueDate, partyCode = doc.Borrower?.PartyCode }),
            lines = doc.Lines.Where(x => x.ItemInstance == null || !x.ItemInstance.IsDeleted).Select(x => new { itemCategoryCode = x.ItemInstance?.Item?.Category?.CategoryCode, item = x.ItemInstance?.Item?.ItemCode, serial = x.ItemInstance?.SerialNumber, ownerName = x.ItemInstance != null ? x.ItemInstance.OwnerName : null, fromBin = x.FromBinLocation?.BinCode, targetBin = x.TargetBinLocation?.BinCode ?? x.TargetExternalLocation, returned = x.IsReturned, condition = x.IsReturned ? "Returned" : "LentOut", returnedAt = x.ReturnedAt, note = x.Note }),
            history
        };
    }

    // ─── Shared helpers ──────────────────────────────────────

    private object Header(ERP.Inventory.Domain.Common.DocumentBase doc, string language, string? warehouse, string? party, object? extra = null)
    {
        return new { doc.Id, EntityName = doc.GetType().Name, doc.DocumentNo, doc.DocumentDate, warehouse, party, status = LocalizationCatalog.EnumText(language, doc.Status), doc.CreatedBy, doc.ApprovedBy, doc.ApprovedAt, doc.PostedAt, doc.Note, extra };
    }

    private static object BuildPdfData(string type, object detail, string language, string? action, DateTime printDate)
    {
        var node = JsonSerializer.SerializeToNode(detail, new JsonSerializerOptions(JsonSerializerDefaults.Web))?.AsObject() ?? new JsonObject();
        var header = node["header"]?.AsObject() ?? new JsonObject();
        var lines = node["lines"] as JsonArray ?? new JsonArray();
        var history = node["history"] as JsonArray ?? new JsonArray();
        var normalizedAction = NormalizePdfAction(type, action);
        var historyForDate = history
            .OfType<JsonObject>()
            .Where(x => SameBusinessDate(ReadDate(x, "timestamp"), printDate))
            .Where(x => HistoryMatchesAction(x, normalizedAction))
            .ToArray();

        var filteredLines = FilterPdfLinesByHistory(lines, historyForDate);
        if (filteredLines.Count == 0)
        {
            filteredLines = FilterPdfLinesByAction(type, lines);
        }
        if (filteredLines.Count == 0)
        {
            filteredLines = lines.OfType<JsonObject>().Select(CloneObject).ToList();
        }

        var rows = filteredLines
            .OrderBy(x => ReadDate(x, "operationDate") ?? ReadDate(header, "documentDate") ?? printDate)
            .ThenBy(x => ReadString(x, "item") ?? ReadString(x, "itemCode") ?? string.Empty)
            .ThenBy(x => ReadString(x, "serial") ?? ReadString(x, "serialNumber") ?? ReadString(x, "snCode") ?? string.Empty)
            .ToArray();
        var historyForAction = history.OfType<JsonObject>()
            .Where(x => HistoryMatchesAction(x, normalizedAction))
            .ToArray();
        var historyRows = historyForDate.Length > 0
            ? historyForDate
            : historyForAction.Length > 0
                ? historyForAction
                : history.OfType<JsonObject>().ToArray();
        EnrichPdfHeader(normalizedAction, header, rows, historyRows);
        foreach (var row in rows)
        {
            EnrichPdfRow(normalizedAction, row, historyRows);
        }

        return new
        {
            templateType = normalizedAction,
            language,
            printDate,
            fileName = $"{normalizedAction}_{ReadString(header, "documentNo") ?? idText(header)}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf",
            header,
            rows,
            historyRows,
            borrowText = BuildBorrowPdfText(language)
        };
    }

    private static void EnrichPdfHeader(string type, JsonObject header, IReadOnlyCollection<JsonObject> rows, IReadOnlyCollection<JsonObject> historyRows)
    {
        var extra = header["extra"] as JsonObject ?? new JsonObject();
        header["extra"] = extra;
        var historyParty = HistoryParty(type, historyRows);
        var historyCardNo = PartyCode(historyParty);
        var historyPartyName = PartyName(historyParty);
        var historyDepartment = FirstHistoryText(historyRows, "borrowDepartment", "receiverDepartment", "department");
        var historyPhone = FirstHistoryText(historyRows, "borrowerPhone", "receiverPhone", "phone");
        var historyPurpose = FirstHistoryText(historyRows, "note", "reason", "repairResultNote");
        var party = type switch
        {
            "inbound" => FirstText(historyPartyName, ReadString(header, "senderName"), ReadString(header, "partyName"), ReadString(header, "party"), ReadString(header, "operatorName"), ReadString(header, "sourceExternalPartyName"), ReadString(header, "createdBy")),
            "repair-send" => FirstText(historyPartyName, ReadString(header, "senderName"), ReadString(header, "repairSenderName"), ReadString(header, "operatorName"), ReadString(header, "createdBy")),
            "repair-receive" => FirstText(historyPartyName, ReadString(header, "receiverName"), ReadString(header, "repairReceiverName"), ReadString(header, "operatorName"), ReadString(header, "createdBy")),
            "borrow-lend" => FirstText(historyPartyName, ReadString(header, "borrowerName"), ReadString(header, "partyName"), ReadString(header, "party"), ReadString(header, "operatorName"), ReadString(header, "createdBy")),
            "borrow-return" => FirstText(historyPartyName, ReadString(header, "returnerName"), ReadString(header, "borrowerName"), ReadString(header, "partyName"), ReadString(header, "party"), ReadString(header, "operatorName"), ReadString(header, "createdBy")),
            "move" or "adjustment" => FirstText(historyPartyName, ReadString(header, "operatorName"), ReadString(header, "partyName"), ReadString(header, "createdBy")),
            "inventory-check" => FirstText(historyPartyName, ReadString(header, "operatorName"), ReadString(header, "partyName"), ReadString(header, "party"), ReadString(header, "createdBy")),
            _ => FirstText(ReadString(header, "partyName"), ReadString(header, "operatorName"), ReadString(header, "receiverName"), ReadString(header, "senderName"), ReadString(header, "borrowerName"), ReadString(header, "returnerName"), ReadString(header, "party"), ReadString(header, "createdBy"))
        };

        SetText(header, "party", party);
        SetText(header, "partyName", party);
        if (type == "inbound") SetIfMissing(header, "senderName", party);
        if (type == "repair-send") SetIfMissing(header, "senderName", party);
        if (type == "repair-receive") SetIfMissing(header, "receiverName", party);
        if (type == "borrow-lend") SetIfMissing(header, "borrowerName", party);
        if (type == "borrow-return") SetIfMissing(header, "returnerName", party);

        SetTextOrMissing(header, "cardNo", FirstText(historyCardNo, ReadString(header, "cardNo"), ReadString(extra, "cardNo"), ReadString(header, "employeeNo"), ReadString(extra, "employeeNo"), ReadString(header, "employeeCode"), ReadString(extra, "employeeCode"), ReadString(header, "operatorCode"), ReadString(extra, "operatorCode"), ReadString(header, "borrowerCode"), ReadString(extra, "borrowerCode"), ReadString(header, "returnerCode"), ReadString(extra, "returnerCode"), ReadString(header, "senderCode"), ReadString(extra, "senderCode"), ReadString(header, "receiverCode"), ReadString(extra, "receiverCode"), ReadString(extra, "partyCode")), !string.IsNullOrWhiteSpace(historyCardNo));
        SetTextOrMissing(header, "department", FirstText(historyDepartment, ReadString(header, "department"), ReadString(extra, "department"), ReadString(header, "departmentName"), ReadString(extra, "departmentName"), ReadString(header, "borrowDepartment"), ReadString(extra, "borrowDepartment"), ReadString(header, "operatorDepartment"), ReadString(extra, "operatorDepartment"), ReadString(header, "senderDepartment"), ReadString(extra, "senderDepartment"), ReadString(header, "receiverDepartment"), ReadString(extra, "receiverDepartment")), !string.IsNullOrWhiteSpace(historyDepartment));
        SetTextOrMissing(header, "purpose", FirstText(historyPurpose, ReadString(header, "purpose"), ReadString(extra, "purpose"), ReadString(header, "borrowPurpose"), ReadString(extra, "borrowPurpose"), ReadString(header, "reason"), ReadString(extra, "reason"), ReadString(header, "note")), !string.IsNullOrWhiteSpace(historyPurpose));
        SetIfMissing(header, "warehouseName", FirstText(ReadString(header, "warehouseName"), ReadString(header, "warehouse"), ReadString(header, "toWarehouseName"), ReadString(header, "toWarehouse")));
        SetIfMissing(header, "ownerName", DistinctOwners(rows));
        SetTextOrMissing(header, "phone", FirstText(historyPhone, ReadString(header, "phone"), ReadString(extra, "phone"), ReadString(header, "borrowerPhone"), ReadString(extra, "borrowerPhone"), ReadString(header, "partyPhone"), ReadString(header, "receiverPhone"), ReadString(extra, "receiverPhone"), ReadString(header, "senderPhone"), ReadString(extra, "senderPhone")), !string.IsNullOrWhiteSpace(historyPhone));

        CopyExtraToHeader(header, extra, "dueDate");
        CopyExtraToHeader(header, extra, "borrowDepartment");
        CopyExtraToHeader(header, extra, "borrowerPhone");
        CopyExtraToHeader(header, extra, "departmentOwner");
        CopyExtraToHeader(header, extra, "partyCode");
    }

    private static void EnrichPdfRow(string type, JsonObject row, IReadOnlyCollection<JsonObject> historyRows)
    {
        var history = FindHistory(row, historyRows);
        SetIfMissing(row, "itemCode", FirstText(ReadString(row, "itemCode"), ReadString(row, "item"), ReadString(row, "itemName"), ReadString(row, "pn"), ReadString(row, "partNo"), ReadString(history, "itemCode")));
        SetIfMissing(row, "serialNumber", FirstText(ReadString(row, "serialNumber"), ReadString(row, "serial"), ReadString(row, "sn"), ReadString(row, "snCode"), ReadString(row, "barcode"), ReadString(history, "serialNumber"), ReadString(history, "snCode")));
        if (row["quantity"] == null && row["qty"] == null && row["quantityDelta"] == null)
        {
            row["quantity"] = 1;
        }
        SetIfMissing(row, "ownerName", FirstText(ReadString(row, "ownerName"), ReadString(row, "owner"), ReadString(row, "itemOwner"), ReadString(row, "itemInstanceOwnerName"), ReadString(row, "ownerDepartment")));
        var historyFrom = ReadString(history, "oldLocation");
        var historyTo = ReadString(history, "newLocation");
        SetTextOrMissing(row, "fromBinCode", FirstText(historyFrom, ReadString(row, "fromBinCode"), ReadString(row, "fromBin"), ReadString(row, "from"), ReadString(row, "fromBinName"), ReadString(row, "fromLocation"), ReadString(row, "oldBinCode"), ReadString(row, "oldLocation")), !string.IsNullOrWhiteSpace(historyFrom));
        SetTextOrMissing(row, "toBinCode", FirstText(historyTo, ReadString(row, "toBinCode"), ReadString(row, "targetBin"), ReadString(row, "to"), ReadString(row, "toBinName"), ReadString(row, "toLocation"), ReadString(row, "newBinCode"), ReadString(row, "newLocation"), ReadString(row, "bin"), ReadString(row, "binCode"), ReadString(row, "binLocationCode"), ReadString(row, "location")), !string.IsNullOrWhiteSpace(historyTo));
        SetTextOrMissing(row, "binCode", FirstText(historyTo, ReadString(row, "binCode"), ReadString(row, "bin"), ReadString(row, "binLocationCode"), ReadString(row, "location"), ReadString(row, "targetBin"), ReadString(row, "to")), !string.IsNullOrWhiteSpace(historyTo));
        SetTextOrMissing(row, "currentLocation", FirstText(historyTo, ReadString(row, "currentLocation"), ReadString(row, "lineText"), ReadString(row, "moveLine"), ReadString(row, "binCode"), ReadString(row, "bin"), ReadString(row, "binLocationCode"), ReadString(row, "location")), !string.IsNullOrWhiteSpace(historyTo));

        var lineText = type switch
        {
            "borrow-lend" or "repair-send" => $"{FirstText(ReadString(row, "fromBinCode"), ReadString(row, "currentLocation"))} --> 仓外 / Ngoài kho",
            "borrow-return" or "repair-receive" => $"仓外 / Ngoài kho --> {FirstText(ReadString(row, "toBinCode"), ReadString(row, "currentLocation"))}",
            "move" when !string.IsNullOrWhiteSpace(FirstText(ReadString(row, "fromBinCode"), ReadString(row, "toBinCode"))) => $"{ReadString(row, "fromBinCode")} --> {ReadString(row, "toBinCode")}",
            _ => FirstText(ReadString(row, "lineText"), ReadString(row, "moveLine"), ReadString(row, "currentLocation"), ReadString(row, "toBinCode"), ReadString(row, "fromBinCode"))
        };
        SetTextOrMissing(row, "lineText", lineText, !string.IsNullOrWhiteSpace(historyFrom) || !string.IsNullOrWhiteSpace(historyTo));
    }

    private static string NormalizePdfAction(string type, string? action)
    {
        if (!string.IsNullOrWhiteSpace(action))
        {
            return action.Trim();
        }

        return type;
    }

    private static bool HistoryMatchesAction(JsonObject history, string action)
    {
        var actionType = ReadString(history, "actionType") ?? string.Empty;
        return action switch
        {
            "borrow-lend" => actionType.Contains("BorrowIssue", StringComparison.OrdinalIgnoreCase),
            "borrow-return" => actionType.Contains("BorrowReturn", StringComparison.OrdinalIgnoreCase),
            "repair-send" => actionType.Contains("RepairSend", StringComparison.OrdinalIgnoreCase),
            "repair-receive" => actionType.Contains("RepairReceive", StringComparison.OrdinalIgnoreCase),
            _ => true
        };
    }

    private static List<JsonObject> FilterPdfLinesByHistory(JsonArray lines, IReadOnlyCollection<JsonObject> historyForDate)
    {
        if (historyForDate.Count == 0) return new List<JsonObject>();
        var serials = historyForDate
            .Select(x => ReadString(x, "serialNumber") ?? ReadString(x, "snCode"))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (serials.Count == 0) return new List<JsonObject>();

        return lines.OfType<JsonObject>()
            .Where(x => serials.Contains(ReadString(x, "serial") ?? ReadString(x, "serialNumber") ?? ReadString(x, "snCode") ?? string.Empty))
            .Select(CloneObject)
            .ToList();
    }

    private static List<JsonObject> FilterPdfLinesByAction(string type, JsonArray lines)
        => type switch
        {
            "borrow-lend" => lines.OfType<JsonObject>().Where(x => ReadBool(x, "returned") != true).Select(CloneObject).ToList(),
            "borrow-return" => lines.OfType<JsonObject>().Where(x => ReadBool(x, "returned") == true).Select(CloneObject).ToList(),
            _ => new List<JsonObject>()
        };

    private static object BuildBorrowPdfText(string language)
        => new
        {
            vi = LocalizationCatalog.Text("vi", "Pdf.BorrowLend.Body"),
            zh = LocalizationCatalog.Text("zh", "Pdf.BorrowLend.Body"),
            returnVi = LocalizationCatalog.Text("vi", "Pdf.BorrowReturn.Body"),
            returnZh = LocalizationCatalog.Text("zh", "Pdf.BorrowReturn.Body")
        };

    private static string FirstText(params string?[] values)
        => values.Select(x => x?.Trim()).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;

    private static string FirstHistoryText(IEnumerable<JsonObject> rows, params string[] keys)
        => rows.Select(row => FirstText(keys.Select(key => ReadString(row, key)).ToArray()))
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;

    private static string HistoryParty(string type, IEnumerable<JsonObject> rows)
        => type switch
        {
            "inbound" => FirstHistoryText(rows, "receiver", "performedBy"),
            "borrow-lend" or "borrow-return" => FirstHistoryText(rows, "borrower", "performedBy"),
            "repair-send" or "repair-receive" => FirstHistoryText(rows, "performedBy", "repairVendor"),
            "move" or "adjustment" or "inventory-check" => FirstHistoryText(rows, "performedBy"),
            _ => FirstHistoryText(rows, "borrower", "receiver", "sender", "performedBy")
        };

    private static string PartyCode(string? text)
    {
        var value = text?.Trim() ?? string.Empty;
        var dash = value.IndexOf('-');
        return dash > 0 ? value[..dash].Trim() : string.Empty;
    }

    private static string PartyName(string? text)
    {
        var value = text?.Trim() ?? string.Empty;
        var dash = value.IndexOf('-');
        return dash >= 0 && dash < value.Length - 1 ? value[(dash + 1)..].Trim() : value;
    }

    private static void SetIfMissing(JsonObject obj, string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        if (!string.IsNullOrWhiteSpace(ReadString(obj, key))) return;
        obj[key] = value;
    }

    private static void SetText(JsonObject obj, string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        obj[key] = value;
    }

    private static void SetTextOrMissing(JsonObject obj, string key, string? value, bool overwrite)
    {
        if (overwrite)
        {
            SetText(obj, key, value);
            return;
        }

        SetIfMissing(obj, key, value);
    }

    private static void CopyExtraToHeader(JsonObject header, JsonObject extra, string key)
        => SetIfMissing(header, key, ReadString(extra, key));

    private static string DistinctOwners(IEnumerable<JsonObject> rows)
    {
        var owners = rows
            .Select(x => FirstText(ReadString(x, "ownerName"), ReadString(x, "owner"), ReadString(x, "itemOwner"), ReadString(x, "itemInstanceOwnerName"), ReadString(x, "ownerDepartment")))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return string.Join(", ", owners);
    }

    private static JsonObject? FindHistory(JsonObject row, IEnumerable<JsonObject> historyRows)
    {
        var serial = FirstText(ReadString(row, "serialNumber"), ReadString(row, "serial"), ReadString(row, "snCode"), ReadString(row, "barcode"));
        var item = FirstText(ReadString(row, "itemCode"), ReadString(row, "item"));
        return historyRows.FirstOrDefault(x =>
            (!string.IsNullOrWhiteSpace(serial) && string.Equals(serial, FirstText(ReadString(x, "serialNumber"), ReadString(x, "snCode")), StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(item) && string.Equals(item, ReadString(x, "itemCode"), StringComparison.OrdinalIgnoreCase)));
    }

    private static JsonObject CloneObject(JsonObject value)
        => JsonNode.Parse(value.ToJsonString())!.AsObject();

    private static string idText(JsonObject header)
        => ReadString(header, "id") ?? "Document";

    private static string? ReadString(JsonObject? obj, string key)
    {
        if (obj == null) return null;
        if (!obj.TryGetPropertyValue(key, out var node) || node == null) return null;
        if (node is JsonValue value && value.TryGetValue<string>(out var text)) return text;
        return node.ToString();
    }

    private static bool? ReadBool(JsonObject obj, string key)
    {
        if (!obj.TryGetPropertyValue(key, out var node) || node == null) return null;
        return node is JsonValue value && value.TryGetValue<bool>(out var result) ? result : null;
    }

    private static DateTime? ReadDate(JsonObject obj, string key)
    {
        var text = ReadString(obj, key);
        return DateTime.TryParse(text, out var value) ? value : null;
    }

    private static bool SameBusinessDate(DateTime? value, DateTime printDate)
        => value.HasValue && value.Value.Date == printDate.Date;

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
