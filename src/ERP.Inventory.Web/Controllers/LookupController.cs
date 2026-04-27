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
            .OrderBy(x => x.WarehouseCode)
            .Select(x => new { id = x.Id, code = x.WarehouseCode, text = x.WarehouseCode + " - " + x.Name })
            .ToArrayAsync(cancellationToken);
        return Json(rows);
    }

    [HttpGet("Categories")]
    public async Task<IActionResult> Categories(CancellationToken cancellationToken)
    {
        var rows = await _db.ItemCategories.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.CategoryCode)
            .Select(x => new { id = x.Id, text = x.CategoryCode + " - " + x.Name })
            .ToArrayAsync(cancellationToken);
        return Json(rows);
    }

    [HttpGet("Items")]
    public async Task<IActionResult> Items([FromQuery] string? keyword, CancellationToken cancellationToken)
    {
        var language = User.FindFirst("language")?.Value ?? "vi";
        var query = _db.Items.AsNoTracking()
            .Include(x => x.Category)
            .Include(x => x.Translations)
            .Where(x => x.IsActive);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var key = keyword.Trim();
            query = query.Where(x => x.ItemCode.Contains(key) || x.DefaultName.Contains(key));
        }

        var rows = await query
            .OrderBy(x => x.ItemCode)
            .Take(100)
            .Select(x => new
            {
                id = x.Id,
                code = x.ItemCode,
                categoryId = x.CategoryId,
                categoryCode = x.Category != null ? x.Category.CategoryCode : string.Empty,
                categoryName = x.Category != null ? x.Category.Name : string.Empty,
                text = x.ItemCode + " - " + (x.Translations
                    .Where(t => t.LanguageCode == language && t.FieldName == "DefaultName")
                    .Select(t => t.Value)
                    .FirstOrDefault() ?? x.DefaultName) + (x.Category != null ? " (" + x.Category.CategoryCode + ")" : string.Empty),
                isSerialManaged = x.IsSerialManaged
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

        var statusFilter = new HashSet<ItemStatus>();
        if (status.HasValue)
        {
            statusFilter.Add(status.Value);
        }

        if (!string.IsNullOrWhiteSpace(statuses))
        {
            foreach (var rawStatus in statuses.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (Enum.TryParse<ItemStatus>(rawStatus, true, out var parsedStatus))
                {
                    statusFilter.Add(parsedStatus);
                }
            }
        }

        if (statusFilter.Count > 0)
        {
            query = query.Where(x => statusFilter.Contains(x.ItemInstance!.Status));
        }

        if (warehouseId.HasValue)
        {
            query = user.CanAccessWarehouse(warehouseId.Value)
                ? query.Where(x => x.WarehouseId == warehouseId.Value)
                : query.Where(x => false);
        }
        else if (!user.IsAdmin)
        {
            query = query.Where(x => x.WarehouseId.HasValue && user.WarehouseIds.Contains(x.WarehouseId.Value));
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var key = keyword.Trim();
            query = query.Where(x =>
                x.ItemInstance!.Item!.ItemCode.Contains(key) ||
                x.ItemInstance.Item.DefaultName.Contains(key) ||
                (x.ItemInstance.SerialNumber != null && x.ItemInstance.SerialNumber.Contains(key)) ||
                (x.ItemInstance.Barcode != null && x.ItemInstance.Barcode.Contains(key)));
        }

        var rows = await query.OrderBy(x => x.ItemInstance!.Item!.ItemCode)
            .Take(100)
            .Select(x => new
            {
                id = x.ItemInstanceId,
                text = x.ItemInstance!.Item!.ItemCode + " - " + (x.ItemInstance.SerialNumber ?? x.ItemInstance.Barcode ?? x.ItemInstance.Item.DefaultName),
                itemCode = x.ItemInstance.Item.ItemCode,
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
    public async Task<IActionResult> Bins([FromQuery] int? warehouseId, [FromQuery] bool availableOnly = false, [FromQuery] int? exceptItemInstanceId = null, [FromQuery] bool includeOccupancy = false, CancellationToken cancellationToken = default)
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

        if (availableOnly)
        {
            query = query.Where(x => !_db.CurrentItemLocations.Any(c =>
                c.BinLocationId == x.Id &&
                (!exceptItemInstanceId.HasValue || c.ItemInstanceId != exceptItemInstanceId.Value) &&
                c.ItemInstance != null &&
                c.ItemInstance.IsActive &&
                c.ItemInstance.Status != ItemStatus.Lost &&
                c.ItemInstance.Status != ItemStatus.Disposed));
        }

        var bins = await query.OrderBy(x => x.FullPath)
            .Take(200)
            .Select(x => new { id = x.Id, text = x.FullPath, warehouseId = x.WarehouseId })
            .ToArrayAsync(cancellationToken);

        if (!includeOccupancy)
        {
            return Json(bins.Select(x => new { x.id, x.text, x.warehouseId }));
        }

        var binIds = bins.Select(x => x.id).ToArray();
        var occupiedRows = await _db.CurrentItemLocations.AsNoTracking()
            .Include(x => x.ItemInstance)!.ThenInclude(x => x!.Item)
            .Where(x =>
                x.BinLocationId.HasValue &&
                binIds.Contains(x.BinLocationId.Value) &&
                x.ItemInstance != null &&
                x.ItemInstance.IsActive &&
                x.ItemInstance.Status != ItemStatus.Lost &&
                x.ItemInstance.Status != ItemStatus.Disposed)
            .Select(x => new
            {
                binId = x.BinLocationId!.Value,
                itemInstanceId = x.ItemInstanceId,
                itemText = x.ItemInstance!.Item!.ItemCode + " - " + (x.ItemInstance.SerialNumber ?? x.ItemInstance.Barcode ?? x.ItemInstance.Item.DefaultName)
            })
            .ToArrayAsync(cancellationToken);

        var occupied = occupiedRows.GroupBy(x => x.binId).ToDictionary(x => x.Key, x => x.First());
        var emptyText = LocalizationCatalog.Text(Language(), "Empty");
        var occupiedText = LocalizationCatalog.Text(Language(), "Occupied by");
        var rows = bins.Select(x =>
        {
            if (!occupied.TryGetValue(x.id, out var item))
            {
                return new { x.id, text = $"{x.text} - {emptyText}", x.warehouseId, isOccupied = false, occupiedItemInstanceId = (int?)null, occupiedItemText = string.Empty };
            }

            return new { x.id, text = $"{x.text} - {occupiedText} {item.itemText}", x.warehouseId, isOccupied = true, occupiedItemInstanceId = (int?)item.itemInstanceId, occupiedItemText = item.itemText };
        }).ToArray();
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

    [HttpGet("Statuses")]
    public IActionResult Statuses()
    {
        return Json(LocalizationCatalog.EnumOptions<ItemStatus>(Language()));
    }

    [HttpGet("RepairResults")]
    public IActionResult RepairResults()
    {
        return Json(LocalizationCatalog.EnumOptions<RepairResult>(Language()));
    }

    [HttpGet("BorrowReturnConditions")]
    public IActionResult BorrowReturnConditions()
    {
        return Json(LocalizationCatalog.EnumOptions<BorrowReturnCondition>(Language()));
    }

    [HttpGet("InventoryCheckResults")]
    public IActionResult InventoryCheckResults()
    {
        return Json(LocalizationCatalog.EnumOptions<InventoryCheckLineResult>(Language()));
    }

    [HttpGet("ExternalPartyTypes")]
    public IActionResult ExternalPartyTypes()
    {
        return Json(LocalizationCatalog.EnumOptions<ExternalPartyType>(Language()));
    }

    [HttpGet("RepairDocuments")]
    public async Task<IActionResult> RepairDocuments(CancellationToken cancellationToken)
    {
        var user = _currentUserService.GetCurrentUser();
        var allowedBinIds = user.IsAdmin
            ? null
            : await _db.BinLocations.AsNoTracking()
                .Where(x => user.WarehouseIds.Contains(x.WarehouseId))
                .Select(x => x.Id)
                .ToArrayAsync(cancellationToken);

        var query = _db.RepairDocuments.AsNoTracking()
            .Include(x => x.RepairVendor)
            .Include(x => x.Lines)
            .Where(x => x.ReceiveResult == null && x.Lines.Any());
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
        var user = _currentUserService.GetCurrentUser();
        var allowedBinIds = user.IsAdmin
            ? null
            : await _db.BinLocations.AsNoTracking()
                .Where(x => user.WarehouseIds.Contains(x.WarehouseId))
                .Select(x => x.Id)
                .ToArrayAsync(cancellationToken);

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

    private string Language()
    {
        return User.FindFirst("language")?.Value ?? "vi";
    }
}
