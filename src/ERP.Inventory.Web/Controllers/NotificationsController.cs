using ERP.Inventory.Infrastructure.Data;
using ERP.Inventory.Application.Interfaces;
using ERP.Inventory.Domain.Entities;
using ERP.Inventory.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using static ERP.Inventory.Web.Controllers.AppController;

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

    [HttpPost("Unread")]
    public async Task<IActionResult> Unread([FromBody] SetLanguageRequest request, CancellationToken cancellationToken)
    {
        var language = NormalizeLanguage(request.Language);
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        await EnsureStocktakeRemindersAsync(userId, cancellationToken);

        var rows = await _db.Notifications.AsNoTracking()
            .Where(x => x.UserId == userId && !x.IsRead)
            .OrderByDescending(x => x.CreatedAt)
            .Take(20)
            .Select(x => new
            {
                x.Id,
                x.Title,
                Message = language == "en" ? x.Message_En : language == "zh" ? x.Message_Zh : x.Message_Vi,
                x.LinkUrl,
                x.CreatedAt
            })
            .ToArrayAsync(cancellationToken);
        return Json(rows);
    }

    [HttpPost("MarkRead/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(int id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var row = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);
        if (row == null) return NotFound();

        row.IsRead = true;
        await _db.SaveChangesAsync(cancellationToken);
        return Json(new { success = true });
    }

    // ─── Private helpers ─────────────────────────────────────

    private static string NormalizeLanguage(string? language)
    {
        return language?.ToLowerInvariant() switch
        {
            "en" => "en",
            "zh" => "zh",
            _ => "vi"
        };
    }

    private async Task EnsureStocktakeRemindersAsync(string userId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId)) return;

        var user = _currentUserService.GetCurrentUser();
        var dueBefore = DateTime.UtcNow.Date.AddDays(-30);
        var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);

        var warehouseQuery = _db.Warehouses.AsNoTracking().Where(x => x.IsActive);
        if (!user.IsAdmin)
        {
            warehouseQuery = warehouseQuery.Where(x => user.WarehouseIds.Contains(x.Id));
        }

        var warehouses = await warehouseQuery.OrderBy(x => x.WarehouseCode).ToArrayAsync(cancellationToken);

        // Batch: get last check dates for all warehouses in one query
        var warehouseIds = warehouses.Select(x => x.Id).ToArray();
        var lastChecks = await _db.InventoryCheckDocuments.AsNoTracking()
            .Where(x => warehouseIds.Contains(x.WarehouseId))
            .GroupBy(x => x.WarehouseId)
            .Select(g => new { WarehouseId = g.Key, LastDate = g.Max(x => x.DocumentDate) })
            .ToDictionaryAsync(x => x.WarehouseId, x => x.LastDate, cancellationToken);

        // Batch: get recent notification warehouse codes in one query
        var recentNotifications = await _db.Notifications.AsNoTracking()
            .Where(x => x.UserId == userId && x.Title == "Stocktake schedule reminder" && x.CreatedAt >= sevenDaysAgo)
            .Select(x => x.Message_En)
            .ToArrayAsync(cancellationToken);

        foreach (var warehouse in warehouses)
        {
            if (lastChecks.TryGetValue(warehouse.Id, out var lastCheck) && lastCheck.Date > dueBefore)
            {
                continue;
            }

            if (recentNotifications.Any(msg => msg.Contains(warehouse.WarehouseCode)))
            {
                continue;
            }

            _db.Notifications.Add(new Notification
            {
                UserId = userId,
                Title = "Stocktake schedule reminder",
                Message_Vi = string.Format(LocalizationCatalog.Text("vi", "Warehouse {0} is due for quarterly stocktake."), warehouse.WarehouseCode),
                Message_En = string.Format(LocalizationCatalog.Text("en", "Warehouse {0} is due for quarterly stocktake."), warehouse.WarehouseCode),
                Message_Zh = string.Format(LocalizationCatalog.Text("zh", "Warehouse {0} is due for quarterly stocktake."), warehouse.WarehouseCode),
                LinkUrl = "/?screen=inventory-check",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
