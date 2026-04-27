using ERP.Inventory.Application.Common;
using ERP.Inventory.Application.Interfaces;
using ERP.Inventory.Domain.Enums;
using ERP.Inventory.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ERP.Inventory.Web.Controllers;

[Authorize]
[Route("[controller]")]
public sealed class ReportsController : Controller
{
    private readonly InventoryDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly ITrackingService _trackingService;

    public ReportsController(InventoryDbContext db, ICurrentUserService currentUserService, ITrackingService trackingService)
    {
        _db = db;
        _currentUserService = currentUserService;
        _trackingService = trackingService;
    }

    [HttpGet("InventoryPreview")]
    public async Task<IActionResult> InventoryPreview([FromQuery] string? keyword, [FromQuery] int? warehouseId, [FromQuery] int? categoryId, [FromQuery] string? status, CancellationToken cancellationToken)
    {
        var result = await _trackingService.GetInventoryListAsync(keyword, warehouseId, categoryId, status, 1, 15, _currentUserService.GetCurrentUser(), cancellationToken);
        return Json(result);
    }

    [HttpGet("HistoryPreview")]
    public async Task<IActionResult> HistoryPreview([FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate, [FromQuery] int? warehouseId, [FromQuery] int? categoryId, [FromQuery] string? status, [FromQuery] string? keyword, CancellationToken cancellationToken)
    {
        var user = _currentUserService.GetCurrentUser();
        var query = _db.ItemMovementHistories.AsNoTracking()
            .Include(x => x.ItemInstance)!.ThenInclude(x => x!.Item)!.ThenInclude(x => x!.Translations)
            .AsQueryable();

        if (fromDate.HasValue)
        {
            query = query.Where(x => x.PerformedAt >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            var to = toDate.Value.Date.AddDays(1);
            query = query.Where(x => x.PerformedAt < to);
        }

        if (warehouseId.HasValue)
        {
            query = query.Where(x => _db.CurrentItemLocations.Any(c => c.ItemInstanceId == x.ItemInstanceId && c.WarehouseId == warehouseId.Value));
        }

        if (categoryId.HasValue)
        {
            query = query.Where(x => x.ItemInstance != null && x.ItemInstance.Item != null && x.ItemInstance.Item.CategoryId == categoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ItemStatus>(status, true, out var parsedStatus))
        {
            query = query.Where(x => x.OldStatus == parsedStatus || x.NewStatus == parsedStatus);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var key = keyword.Trim();
            query = query.Where(x =>
                x.DocumentNo.Contains(key) ||
                x.PerformedBy.Contains(key) ||
                (x.Note != null && x.Note.Contains(key)) ||
                (x.ItemInstance != null && x.ItemInstance.SerialNumber != null && x.ItemInstance.SerialNumber.Contains(key)) ||
                (x.ItemInstance != null && x.ItemInstance.Item != null && (x.ItemInstance.Item.ItemCode.Contains(key) || x.ItemInstance.Item.DefaultName.Contains(key))));
        }

        if (!user.IsAdmin)
        {
            query = query.Where(x => _db.CurrentItemLocations.Any(c =>
                c.ItemInstanceId == x.ItemInstanceId &&
                (c.WarehouseId == null || user.WarehouseIds.Contains(c.WarehouseId.Value))));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(x => x.PerformedAt)
            .Take(25)
            .Select(x => new
            {
                x.PerformedAt,
                ItemCode = x.ItemInstance != null && x.ItemInstance.Item != null ? x.ItemInstance.Item.ItemCode : string.Empty,
                ItemName = x.ItemInstance != null && x.ItemInstance.Item != null ? x.ItemInstance.Item.DefaultName : string.Empty,
                SerialNumber = x.ItemInstance != null ? x.ItemInstance.SerialNumber : null,
                ActionType = x.ActionType,
                x.OldStatus,
                x.NewStatus,
                x.DocumentNo,
                x.PerformedBy
            })
            .ToArrayAsync(cancellationToken);

        return Json(ServiceResult<PagedResult<object>>.Ok(new PagedResult<object>
        {
            Items = rows.Cast<object>().ToArray(),
            Page = 1,
            PageSize = 25,
            TotalCount = total
        }));
    }
}
