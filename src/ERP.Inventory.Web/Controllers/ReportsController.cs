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
        var result = await _trackingService.GetListInventoryAsync(keyword, warehouseId, categoryId, status, 1, 0, _currentUserService.GetCurrentUser(), cancellationToken);
        return Json(result);
    }

    [HttpGet("HistoryPreview")]
    public async Task<IActionResult> HistoryPreview([FromQuery] DateTime? fromDate,  [FromQuery] DateTime? toDate,  [FromQuery] int? warehouseId,  [FromQuery] int? categoryId,  [FromQuery] string? status, [FromQuery] string? keyword, CancellationToken cancellationToken)
    {
        var user = _currentUserService.GetCurrentUser();

        var query =
            from h in _db.ItemMovementHistories.AsNoTracking()
            join ii in _db.ItemInstances on h.ItemInstanceId equals ii.Id
            join it in _db.Items on ii.ItemId equals it.Id
            join c in _db.CurrentItemLocations on ii.Id equals c.ItemInstanceId
            where true
            select new { h, ii, it, c };

        if (fromDate.HasValue)
            query = query.Where(x => x.h.PerformedAt >= fromDate.Value);

        if (toDate.HasValue)
        {
            var to = toDate.Value.Date.AddDays(1);
            query = query.Where(x => x.h.PerformedAt < to);
        }

        if (warehouseId.HasValue)
            query = query.Where(x => x.c.WarehouseId == warehouseId.Value);

        if (categoryId.HasValue)
            query = query.Where(x => x.it.CategoryId == categoryId.Value);

        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse<ItemStatus>(status, true, out var parsedStatus))
        {
            query = query.Where(x =>
                x.h.OldStatus == parsedStatus ||
                x.h.NewStatus == parsedStatus);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var key = keyword.Trim();
            query = query.Where(x =>
                x.h.DocumentNo.Contains(key) ||
                x.h.PerformedBy.Contains(key) ||
                (x.h.Note != null && x.h.Note.Contains(key)) ||
                x.ii.SerialNumber.Contains(key) ||
                x.it.ItemCode.Contains(key) ||
                x.it.DefaultName.Contains(key));
        }

        if (!user.IsAdmin)
        {
            query = query.Where(x =>
                x.c.WarehouseId == null ||
                user.WarehouseIds.Contains(x.c.WarehouseId.Value));
        }

        var total = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderByDescending(x => x.h.PerformedAt)
            .Select(x => new
            {
                x.h.PerformedAt,
                ItemCode = x.it.ItemCode,
                ItemName = x.it.DefaultName,
                SerialNumber = x.ii.SerialNumber,
                x.h.ActionType,
                x.h.OldStatus,
                x.h.NewStatus,
                x.h.DocumentNo,
                x.h.PerformedBy
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
