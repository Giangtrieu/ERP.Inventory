using ERP.Inventory.Application.Common;
using ERP.Inventory.Domain.Entities;
using ERP.Inventory.Domain.Enums;
using ERP.Inventory.Infrastructure.Data;
using ERP.Inventory.Application.Interfaces;
using ERP.Inventory.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ERP.Inventory.Web.Controllers;

[Authorize]
[Route("[controller]")]
public sealed class LookupController : Controller
{
    private readonly InventoryDbContext _db;
    private readonly ICurrentUserService _currentUserService;

    public LookupController(InventoryDbContext db, ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }

    [HttpGet("Warehouses")]
    public async Task<IActionResult> Warehouses(CancellationToken cancellationToken)
    {
        var user = _currentUserService.GetCurrentUser();
        var query = _db.Warehouses.AsNoTracking().Where(x => x.IsActive);
        if (!user.IsAdmin)
        {
            query = query.Where(x => user.WarehouseIds.Contains(x.Id));
        }

        var rows = await query
            //.OrderBy(x => x.WarehouseCode)
            .Select(x => new { id = x.Id, /*code = x.WarehouseCode*/ text = x.WarehouseCode })
            .ToArrayAsync(cancellationToken);
        return Json(rows);
    }

    [HttpGet("Categories")]
    public async Task<IActionResult> Categories(CancellationToken cancellationToken)
    {
        var rows = await _db.ItemCategories.AsNoTracking()
            .Where(x => x.IsActive)
            //.OrderBy(x => x.CategoryCode)
            .Select(x => new { id = x.Id, text = x.CategoryCode})
            .ToArrayAsync(cancellationToken);
        return Json(rows);
    }

    [HttpGet("Items")]
    public async Task<IActionResult> Items([FromQuery] string? keyword, CancellationToken cancellationToken)
    {
        var query = _db.Items.AsNoTracking()
            //.Include(x => x.Category)
            .Where(x => x.IsActive);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var key = keyword.Trim();
            query = query.Where(x => x.ItemCode.Contains(key) || x.DefaultName.Contains(key));
        }

        var rows = await query
            //.OrderBy(x => x.ItemCode)
            .Select(x => new
            {
                id = x.Id,
                //code = x.ItemCode,
                //categoryId = x.CategoryId,
                //categoryCode = x.Category != null ? x.Category.CategoryCode : string.Empty,
                //categoryName = x.Category != null ? x.Category.Name : string.Empty,
                text = x.ItemCode,
                //isSerialManaged = x.IsSerialManaged
            })
            .ToArrayAsync(cancellationToken);

        return Json(rows);
    }

    [HttpGet("ItemInstances")]
    public async Task<IActionResult> ItemInstances([FromQuery] ItemStatus? status, [FromQuery] string? statuses, [FromQuery] int? warehouseId, [FromQuery] string? keyword, CancellationToken cancellationToken)
    {
        var user = _currentUserService.GetCurrentUser();
        var query = _db.CurrentItemLocations.AsNoTracking()
            .Include(x => x.ItemInstance)!.ThenInclude(x => x!.Item)
            .Include(x => x.Warehouse)
            .Include(x => x.BinLocation)
            .Include(x => x.ExternalParty)
            .Where(x => x.ItemInstance != null && x.ItemInstance.IsActive);

        query = ApplyStatusFilter(query, status, statuses);
        query = ApplyWarehouseScope(query, warehouseId, user);
        query = ApplyItemKeywordFilter(query, keyword);

        var rows = await query.OrderBy(x => x.ItemInstance!.Item!.ItemCode)
            .Take(100)
            .Select(x => new
            {
                id = x.ItemInstanceId,
                text = x.ItemInstance!.SerialNumber,
                itemCode = x.ItemInstance.Item!.ItemCode,
                serialNumber = x.ItemInstance.SerialNumber,
                barcode = x.ItemInstance.Barcode,
                status = x.ItemInstance.Status,
                warehouseId = x.WarehouseId,
                binLocationId = x.BinLocationId,
                location = x.BinLocation != null
                    ? x.BinLocation.FullPath
                    : (!string.IsNullOrWhiteSpace(x.ExternalLocationText)
                        ? (x.ExternalParty != null ? x.ExternalParty.Name + " - " + x.ExternalLocationText : x.ExternalLocationText)
                        : (x.ExternalParty != null ? x.ExternalParty.Name : x.LocationType.ToString()))
            })
            .ToArrayAsync(cancellationToken);

        return Json(rows);
    }

    [HttpGet("ItemBorrowReturns")]
    public async Task<IActionResult> ItemBorrowReturns([FromQuery] int? borrowDocumentId, [FromQuery] ItemStatus? status, [FromQuery] string? statuses, [FromQuery] int? warehouseId, [FromQuery] string? keyword, CancellationToken cancellationToken)
    {
        var user = _currentUserService.GetCurrentUser();
        var query = _db.CurrentItemLocations.AsNoTracking()
            .Include(x => x.ItemInstance)!.ThenInclude(x => x!.Item)
            .Include(x => x.Warehouse)
            .Include(x => x.BinLocation)
            .Include(x => x.ExternalParty)
            .Where(x => x.ItemInstance != null && x.ItemInstance.IsActive);

        query = ApplyStatusFilter(query, status, statuses);

        if (borrowDocumentId.HasValue)
        {
            query = from q in query
                    join bdl in _db.BorrowDocumentLines.AsNoTracking() on q.ItemInstanceId equals bdl.ItemInstanceId
                    where bdl.BorrowDocumentId == borrowDocumentId && bdl.IsReturned == false
                    select q;
        }

        query = ApplyWarehouseScope(query, warehouseId, user);
        query = ApplyItemKeywordFilter(query, keyword);

        var rows = await query.OrderBy(x => x.ItemInstance!.Item!.ItemCode)
            .Take(100)
            .Select(x => new
            {
                id = x.ItemInstanceId,
                text = x.ItemInstance!.SerialNumber,
                itemCode = x.ItemInstance.Item!.ItemCode,
                serialNumber = x.ItemInstance.SerialNumber,
                barcode = x.ItemInstance.Barcode,
                status = x.ItemInstance.Status,
                warehouseId = x.WarehouseId,
                binLocationId = x.BinLocationId,
                location = x.BinLocation != null
                    ? x.BinLocation.FullPath
                    : (!string.IsNullOrWhiteSpace(x.ExternalLocationText)
                        ? (x.ExternalParty != null ? x.ExternalParty.Name + " - " + x.ExternalLocationText : x.ExternalLocationText)
                        : (x.ExternalParty != null ? x.ExternalParty.Name : x.LocationType.ToString()))
            })
            .ToArrayAsync(cancellationToken);

        return Json(rows);
    }

    [HttpGet("Bins")]
    public async Task<IActionResult> Bins([FromQuery] int? warehouseId, CancellationToken cancellationToken)
    {
        var user = _currentUserService.GetCurrentUser();
        var query = _db.BinLocations.AsNoTracking().Where(x => x.IsActive);

        if (warehouseId.HasValue)
        {
            query = user.CanAccessWarehouse(warehouseId.Value)
                ? query.Where(x => x.WarehouseId == warehouseId.Value)
                : query.Where(x => false);
        }
        else if (!user.IsAdmin)
        {
            query = query.Where(x => user.WarehouseIds.Contains(x.WarehouseId));
        }

        var rows = await query
            .OrderBy(x => x.BinCode)
            .Select(x => new { x.Id, x.BinCode })
            .ToArrayAsync(cancellationToken);

        return Json(rows);
    }

    [HttpGet("ExternalParties")]
    public async Task<IActionResult> ExternalParties([FromQuery] ExternalPartyType? type, CancellationToken cancellationToken)
    {
        var query = _db.ExternalParties.AsNoTracking().Where(x => x.IsActive);
        if (type.HasValue)
        {
            query = query.Where(x => x.PartyType == type.Value);
        }

        var rows = await query.OrderBy(x => x.PartyCode)
            .Select(x => new { id = x.Id, text = x.PartyCode + " - " + x.Name, type = x.PartyType.ToString() })
            .ToArrayAsync(cancellationToken);
        return Json(rows);
    }

    [HttpGet("DocumentPeriodType")]
    public IActionResult DocumentPeriodType() => Json(LocalizationCatalog.EnumOptions<DocumentPeriodType>(Language()));

    [HttpGet("ItemStatusView")]
    public IActionResult ItemStatusView() => Json(LocalizationCatalog.EnumOptions<ItemStatusView>(Language()));
    [HttpGet("InventoryStatuses")]
    public IActionResult InventoryStatuses() => Json(LocalizationCatalog.EnumOptions<InventoryStatus>(Language()));

    [HttpGet("Statuses")]
    public IActionResult Statuses() => Json(LocalizationCatalog.EnumOptions<ItemStatus>(Language()));

    [HttpGet("RepairResults")]
    public IActionResult RepairResults() => Json(LocalizationCatalog.EnumOptions<RepairResult>(Language()));

    [HttpGet("BorrowReturnConditions")]
    public IActionResult BorrowReturnConditions() => Json(LocalizationCatalog.EnumOptions<BorrowReturnCondition>(Language()));

    [HttpGet("InventoryCheckResults")]
    public IActionResult InventoryCheckResults() => Json(LocalizationCatalog.EnumOptions<InventoryCheckLineResult>(Language()));

    [HttpGet("ExternalPartyTypes")]
    public IActionResult ExternalPartyTypes() => Json(LocalizationCatalog.EnumOptions<ExternalPartyType>(Language()));

    [HttpGet("RepairDocuments")]
    public async Task<IActionResult> RepairDocuments(CancellationToken cancellationToken)
    {
        var allowedBinIds = await AllowedBinIds(cancellationToken);

        var query = _db.RepairDocuments.AsNoTracking()
            .Include(x => x.RepairVendor)
            .Include(x => x.Lines)
            .Where(x => x.Lines.Any(l => !l.IsReturned));

        if (allowedBinIds != null)
        {
            query = query.Where(x => x.Lines.Any(l =>
                (l.FromBinLocationId.HasValue && allowedBinIds.Contains(l.FromBinLocationId.Value)) ||
                (l.TargetBinLocationId.HasValue && allowedBinIds.Contains(l.TargetBinLocationId.Value))));
        }

        var rows = await query
            .OrderByDescending(x => x.DocumentDate)
            .Take(100)
            .Select(x => new
            {
                id = x.Id,
                text = x.DocumentNo + " - " + (x.RepairVendor != null ? x.RepairVendor.Name : "Repair vendor")
            })
            .ToArrayAsync(cancellationToken);
        return Json(rows);
    }

    [HttpGet("BorrowDocuments")]
    public async Task<IActionResult> BorrowDocuments(CancellationToken cancellationToken)
    {
        var allowedBinIds = await AllowedBinIds(cancellationToken);

        var query = _db.BorrowDocuments.AsNoTracking()
            .Include(x => x.Borrower)
            .Include(x => x.Lines)
            .Where(x => x.Lines.Any(l => !l.IsReturned));

        if (allowedBinIds != null)
        {
            query = query.Where(x => x.Lines.Any(l =>
                (l.FromBinLocationId.HasValue && allowedBinIds.Contains(l.FromBinLocationId.Value)) ||
                (l.TargetBinLocationId.HasValue && allowedBinIds.Contains(l.TargetBinLocationId.Value))));
        }

        var rows = await query
            .OrderByDescending(x => x.DocumentDate)
            .Take(100)
            .Select(x => new
            {
                id = x.Id,
                text = x.DocumentNo + " - " + (x.Borrower != null ? x.Borrower.Name : "Borrower")
            })
            .ToArrayAsync(cancellationToken);
        return Json(rows);
    }

    // ─── Shared helpers ──────────────────────────────────────

    private string Language() => User.FindFirst("language")?.Value ?? "vi";

    private async Task<int[]?> AllowedBinIds(CancellationToken cancellationToken)
    {
        var user = _currentUserService.GetCurrentUser();
        if (user.IsAdmin) return null;
        return await _db.BinLocations.AsNoTracking()
            .Where(x => user.WarehouseIds.Contains(x.WarehouseId))
            .Select(x => x.Id)
            .ToArrayAsync(cancellationToken);
    }

    private static IQueryable<CurrentItemLocation> ApplyStatusFilter(IQueryable<CurrentItemLocation> query, ItemStatus? status, string? statuses)
    {
        var statusFilter = new HashSet<ItemStatus>();
        if (status.HasValue) statusFilter.Add(status.Value);
        if (!string.IsNullOrWhiteSpace(statuses))
        {
            foreach (var raw in statuses.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (Enum.TryParse<ItemStatus>(raw, true, out var parsed))
                    statusFilter.Add(parsed);
            }
        }

        return statusFilter.Count > 0
            ? query.Where(x => statusFilter.Contains(x.ItemInstance!.Status))
            : query;
    }

    private static IQueryable<CurrentItemLocation> ApplyWarehouseScope(IQueryable<CurrentItemLocation> query, int? warehouseId, CurrentUserContext user)
    {
        if (warehouseId.HasValue)
        {
            return user.CanAccessWarehouse(warehouseId.Value)
                ? query.Where(x => x.WarehouseId == warehouseId.Value)
                : query.Where(x => false);
        }

        return user.IsAdmin ? query : query.Where(x => x.WarehouseId.HasValue && user.WarehouseIds.Contains(x.WarehouseId.Value));
    }

    private static IQueryable<CurrentItemLocation> ApplyItemKeywordFilter(IQueryable<CurrentItemLocation> query, string? keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword)) return query;

        var key = keyword.Trim();
        return query.Where(x =>
            x.ItemInstance!.Item!.ItemCode.Contains(key) ||
            x.ItemInstance.Item.DefaultName.Contains(key) ||
            (x.ItemInstance.SerialNumber != null && x.ItemInstance.SerialNumber.Contains(key)) ||
            (x.ItemInstance.Barcode != null && x.ItemInstance.Barcode.Contains(key)));
    }
}
