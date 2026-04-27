using ERP.Inventory.Infrastructure.Data;
using ERP.Inventory.Application.Interfaces;
using ERP.Inventory.Domain.Entities;
using ERP.Inventory.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ERP.Inventory.Web.Controllers;

[Authorize]
[Route("[controller]")]
public sealed class NotificationsController : Controller
{
    private readonly InventoryDbContext _db;
    private readonly ICurrentUserService _currentUserService;

    public NotificationsController(InventoryDbContext db, ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }

    [HttpGet("Unread")]
    public async Task<IActionResult> Unread(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        await EnsureStocktakeRemindersAsync(userId, cancellationToken);
        var rows = await _db.Notifications.AsNoTracking()
            .Where(x => x.UserId == userId && !x.IsRead)
            .OrderByDescending(x => x.CreatedAt)
            .Take(20)
            .Select(x => new { x.Id, x.Title, x.Message, x.LinkUrl, x.CreatedAt })
            .ToArrayAsync(cancellationToken);
        return Json(rows);
    }

    private async Task EnsureStocktakeRemindersAsync(string userId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        var user = _currentUserService.GetCurrentUser();
        var language = User.FindFirst("language")?.Value ?? user.LanguageCode ?? "vi";
        var dueBefore = DateTime.Now.Date.AddDays(-90);
        var warehouseQuery = _db.Warehouses.AsNoTracking().Where(x => x.IsActive);
        if (!user.IsAdmin)
        {
            warehouseQuery = warehouseQuery.Where(x => user.WarehouseIds.Contains(x.Id));
        }

        var warehouses = await warehouseQuery.OrderBy(x => x.WarehouseCode).ToArrayAsync(cancellationToken);
        foreach (var warehouse in warehouses)
        {
            var lastCheck = await _db.InventoryCheckDocuments.AsNoTracking()
                .Where(x => x.WarehouseId == warehouse.Id)
                .MaxAsync(x => (DateTime?)x.DocumentDate, cancellationToken);
            if (lastCheck.HasValue && lastCheck.Value.Date > dueBefore)
            {
                continue;
            }

            var title = LocalizationCatalog.Text(language, "Stocktake schedule reminder");
            var recent = await _db.Notifications.AnyAsync(x =>
                x.UserId == userId &&
                x.Title == title &&
                x.Message.Contains(warehouse.WarehouseCode) &&
                x.CreatedAt >= DateTime.Now.AddDays(-7), cancellationToken);
            if (recent)
            {
                continue;
            }

            _db.Notifications.Add(new Notification
            {
                UserId = userId,
                Title = title,
                Message = string.Format(LocalizationCatalog.Text(language, "Warehouse {0} is due for quarterly stocktake."), warehouse.WarehouseCode),
                LinkUrl = "/?screen=inventory-check",
                CreatedAt = DateTime.Now,
                CreatedBy = "system"
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    [HttpPost("MarkRead/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(int id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var row = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);
        if (row == null)
        {
            return NotFound();
        }

        row.IsRead = true;
        await _db.SaveChangesAsync(cancellationToken);
        return Json(new { success = true });
    }
}
