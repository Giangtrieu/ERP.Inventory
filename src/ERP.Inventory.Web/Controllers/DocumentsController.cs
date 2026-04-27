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

    private async Task<object[]> InboundRows(string? keyword, DateTime? fromDate, DateTime? toDate, string language, CancellationToken cancellationToken)
    {
        var query = Scope(_db.InboundDocuments.AsNoTracking().Include(x => x.Warehouse).Include(x => x.SourceExternalParty).Include(x => x.Lines).AsQueryable(), x => x.WarehouseId);
        query = ApplyDocumentFilter(query, keyword, fromDate, toDate);
        return await query.OrderByDescending(x => x.DocumentDate).Take(100)
            .Select(x => new { id = x.Id, documentNo = x.DocumentNo, documentDate = x.DocumentDate, party = x.SourceExternalParty != null ? x.SourceExternalParty.Name : "", warehouse = x.Warehouse != null ? x.Warehouse.WarehouseCode : "", status = LocalizationCatalog.EnumText(language, x.Status), lines = x.Lines.Count, createdBy = x.CreatedBy, approvedBy = x.ApprovedBy, postedAt = x.PostedAt })
            .ToArrayAsync(cancellationToken);
    }

    private async Task<object[]> MoveRows(string? keyword, DateTime? fromDate, DateTime? toDate, string language, CancellationToken cancellationToken)
    {
        var query = Scope(_db.MoveDocuments.AsNoTracking().Include(x => x.Warehouse).Include(x => x.Lines).AsQueryable(), x => x.WarehouseId);
        query = ApplyDocumentFilter(query, keyword, fromDate, toDate);
        return await query.OrderByDescending(x => x.DocumentDate).Take(100)
            .Select(x => new { id = x.Id, documentNo = x.DocumentNo, documentDate = x.DocumentDate, party = "", warehouse = x.Warehouse != null ? x.Warehouse.WarehouseCode : "", status = LocalizationCatalog.EnumText(language, x.Status), lines = x.Lines.Count, createdBy = x.CreatedBy, approvedBy = x.ApprovedBy, postedAt = x.PostedAt })
            .ToArrayAsync(cancellationToken);
    }

    private async Task<object[]> AdjustmentRows(string? keyword, DateTime? fromDate, DateTime? toDate, string language, CancellationToken cancellationToken)
    {
        var query = Scope(_db.AdjustmentDocuments.AsNoTracking().Include(x => x.Warehouse).Include(x => x.Lines).AsQueryable(), x => x.WarehouseId);
        query = ApplyDocumentFilter(query, keyword, fromDate, toDate);
        return await query.OrderByDescending(x => x.DocumentDate).Take(100)
            .Select(x => new { id = x.Id, documentNo = x.DocumentNo, documentDate = x.DocumentDate, party = x.Reason, warehouse = x.Warehouse != null ? x.Warehouse.WarehouseCode : "", status = LocalizationCatalog.EnumText(language, x.Status), lines = x.Lines.Count, createdBy = x.CreatedBy, approvedBy = x.ApprovedBy, postedAt = x.PostedAt })
            .ToArrayAsync(cancellationToken);
    }

    private async Task<object[]> InventoryCheckRows(string? keyword, DateTime? fromDate, DateTime? toDate, string language, CancellationToken cancellationToken)
    {
        var query = Scope(_db.InventoryCheckDocuments.AsNoTracking().Include(x => x.Warehouse).Include(x => x.Lines).AsQueryable(), x => x.WarehouseId);
        query = ApplyDocumentFilter(query, keyword, fromDate, toDate);
        return await query.OrderByDescending(x => x.DocumentDate).Take(100)
            .Select(x => new { id = x.Id, documentNo = x.DocumentNo, documentDate = x.DocumentDate, party = x.ResponsibleStaff, warehouse = x.Warehouse != null ? x.Warehouse.WarehouseCode : "", status = LocalizationCatalog.EnumText(language, x.Status), lines = x.Lines.Count, createdBy = x.CreatedBy, approvedBy = x.ApprovedBy, postedAt = x.PostedAt })
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
            .Select(x => new { id = x.Id, documentNo = x.DocumentNo, documentDate = x.DocumentDate, party = x.RepairVendor != null ? x.RepairVendor.Name : "", warehouse = "", status = x.ReceiveResult == null ? LocalizationCatalog.Text(language, "Open") : LocalizationCatalog.EnumText(language, x.ReceiveResult.Value), lines = x.Lines.Count, createdBy = x.CreatedBy, approvedBy = x.ApprovedBy, postedAt = x.PostedAt })
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
            .Select(x => new { id = x.Id, documentNo = x.DocumentNo, documentDate = x.DocumentDate, party = x.Borrower != null ? x.Borrower.Name : "", warehouse = "", status = x.Lines.Any(l => !l.IsReturned) ? LocalizationCatalog.Text(language, "Open") : LocalizationCatalog.Text(language, "Returned"), lines = x.Lines.Count, createdBy = x.CreatedBy, approvedBy = x.ApprovedBy, postedAt = x.PostedAt })
            .ToArrayAsync(cancellationToken);
    }

    private async Task<object?> InboundDetail(int id, string language, CancellationToken cancellationToken)
    {
        var doc = await Scope(_db.InboundDocuments.AsNoTracking().Include(x => x.Warehouse).Include(x => x.SourceExternalParty).Include(x => x.Lines).ThenInclude(x => x.Item).Include(x => x.Lines).ThenInclude(x => x.BinLocation).AsQueryable(), x => x.WarehouseId).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return doc == null ? null : new { header = Header(doc, language, doc.Warehouse?.WarehouseCode, doc.SourceExternalParty?.Name), lines = doc.Lines.Select(x => new { item = x.Item?.ItemCode, serial = x.SerialNumber, barcode = x.Barcode, bin = x.BinLocation?.FullPath, condition = x.Condition, note = x.Note }) };
    }

    private async Task<object?> MoveDetail(int id, string language, CancellationToken cancellationToken)
    {
        var doc = await Scope(_db.MoveDocuments.AsNoTracking().Include(x => x.Warehouse).Include(x => x.Lines).ThenInclude(x => x.ItemInstance)!.ThenInclude(x => x!.Item).Include(x => x.Lines).ThenInclude(x => x.FromBinLocation).Include(x => x.Lines).ThenInclude(x => x.TargetBinLocation).AsQueryable(), x => x.WarehouseId).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return doc == null ? null : new { header = Header(doc, language, doc.Warehouse?.WarehouseCode, null), lines = doc.Lines.Select(x => new { item = x.ItemInstance?.Item?.ItemCode, serial = x.ItemInstance?.SerialNumber, from = x.FromBinLocation?.FullPath, to = x.TargetBinLocation?.FullPath, note = x.Note }) };
    }

    private async Task<object?> AdjustmentDetail(int id, string language, CancellationToken cancellationToken)
    {
        var doc = await Scope(_db.AdjustmentDocuments.AsNoTracking().Include(x => x.Warehouse).Include(x => x.Lines).ThenInclude(x => x.ItemInstance)!.ThenInclude(x => x!.Item).AsQueryable(), x => x.WarehouseId).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return doc == null ? null : new { header = Header(doc, language, doc.Warehouse?.WarehouseCode, doc.Reason), lines = doc.Lines.Select(x => new { item = x.ItemInstance?.Item?.ItemCode, serial = x.ItemInstance?.SerialNumber, oldStatus = LocalizationCatalog.EnumText(language, x.OldStatus), newStatus = LocalizationCatalog.EnumText(language, x.NewStatus), reason = x.Reason }) };
    }

    private async Task<object?> InventoryCheckDetail(int id, string language, CancellationToken cancellationToken)
    {
        var doc = await Scope(_db.InventoryCheckDocuments.AsNoTracking().Include(x => x.Warehouse).Include(x => x.Lines).ThenInclude(x => x.ItemInstance)!.ThenInclude(x => x!.Item).AsQueryable(), x => x.WarehouseId).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return doc == null ? null : new { header = Header(doc, language, doc.Warehouse?.WarehouseCode, doc.ResponsibleStaff), lines = doc.Lines.Select(x => new { item = x.ItemInstance?.Item?.ItemCode, serial = x.ItemInstance?.SerialNumber, result = LocalizationCatalog.EnumText(language, x.Result), note = x.Note }) };
    }

    private async Task<object?> RepairDetail(int id, string language, CancellationToken cancellationToken)
    {
        var allowedBinIds = await AllowedBinIds(cancellationToken);
        var doc = await _db.RepairDocuments.AsNoTracking().Include(x => x.RepairVendor).Include(x => x.Lines).ThenInclude(x => x.ItemInstance)!.ThenInclude(x => x!.Item).Include(x => x.Lines).ThenInclude(x => x.FromBinLocation).Include(x => x.Lines).ThenInclude(x => x.TargetBinLocation).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (doc == null || (allowedBinIds != null && !doc.Lines.Any(x =>
            (x.FromBinLocationId.HasValue && allowedBinIds.Contains(x.FromBinLocationId.Value)) ||
            (x.TargetBinLocationId.HasValue && allowedBinIds.Contains(x.TargetBinLocationId.Value))))) return null;
        return new { header = Header(doc, language, null, doc.RepairVendor?.Name), lines = doc.Lines.Select(x => new { item = x.ItemInstance?.Item?.ItemCode, serial = x.ItemInstance?.SerialNumber, fromBin = x.FromBinLocation?.FullPath, targetBin = x.TargetBinLocation?.FullPath ?? x.TargetExternalLocation, newSerial = x.NewSerialNumber, note = x.RepairResultNote }) };
    }

    private async Task<object?> BorrowDetail(int id, string language, CancellationToken cancellationToken)
    {
        var allowedBinIds = await AllowedBinIds(cancellationToken);
        var doc = await _db.BorrowDocuments.AsNoTracking().Include(x => x.Borrower).Include(x => x.Lines).ThenInclude(x => x.ItemInstance)!.ThenInclude(x => x!.Item).Include(x => x.Lines).ThenInclude(x => x.FromBinLocation).Include(x => x.Lines).ThenInclude(x => x.TargetBinLocation).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (doc == null || (allowedBinIds != null && !doc.Lines.Any(x =>
            (x.FromBinLocationId.HasValue && allowedBinIds.Contains(x.FromBinLocationId.Value)) ||
            (x.TargetBinLocationId.HasValue && allowedBinIds.Contains(x.TargetBinLocationId.Value))))) return null;
        return new
        {
            header = Header(doc, language, null, doc.Borrower?.Name, new { doc.Purpose, doc.BorrowDepartment, doc.BorrowerPhone, doc.DepartmentOwner, doc.DueDate }),
            lines = doc.Lines.Select(x => new { item = x.ItemInstance?.Item?.ItemCode, serial = x.ItemInstance?.SerialNumber, fromBin = x.FromBinLocation?.FullPath, targetBin = x.TargetBinLocation?.FullPath ?? x.TargetExternalLocation, returned = x.IsReturned, condition = x.ReturnCondition.HasValue ? LocalizationCatalog.EnumText(language, x.ReturnCondition.Value) : "", returnedAt = x.ReturnedAt, note = x.Note })
        };
    }

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
        return await _db.BinLocations.AsNoTracking().Where(x => user.WarehouseIds.Contains(x.WarehouseId)).Select(x => x.Id).ToArrayAsync(cancellationToken);
    }

    private string Language() => User.FindFirst("language")?.Value ?? "vi";
}
