using ERP.Inventory.Application.Interfaces;
using ERP.Inventory.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ERP.Inventory.Web.Controllers;

[Authorize]
[Route("[controller]")]
public sealed class AppController : Controller
{
    private readonly ICurrentUserService _currentUserService;
    private readonly InventoryDbContext _db;

    public AppController(ICurrentUserService currentUserService, InventoryDbContext db)
    {
        _currentUserService = currentUserService;
        _db = db;
    }

    [HttpGet("Bootstrap")]
    public async Task<IActionResult> Bootstrap(CancellationToken cancellationToken)
    {
        var user = _currentUserService.GetCurrentUser();
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var displayName = User.FindFirstValue("display_name") ?? user.UserName;
        var unread = await _db.Notifications.CountAsync(x => x.UserId == userId && !x.IsRead, cancellationToken);

        return Json(new
        {
            user = new
            {
                id = user.UserId,
                userName = user.UserName,
                displayName,
                language = user.LanguageCode,
                roles = user.Roles,
                warehouseIds = user.WarehouseIds
            },
            permissions = new
            {
                canManage = user.CanManage,
                canOperate = user.CanOperate,
                canView = true
            },
            notifications = new { unread }
        });
    }

    [HttpPost("Language")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetLanguage([FromBody] SetLanguageRequest request, CancellationToken cancellationToken)
    {
        var language = NormalizeLanguage(request.Language);
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var user = await _db.SystemUsers.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user == null)
        {
            return NotFound();
        }

        user.PreferredLanguage = language;
        await _db.SaveChangesAsync(cancellationToken);

        var claims = User.Claims.Where(x => x.Type != "language").ToList();
        claims.Add(new Claim("language", language));
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

        return Json(new { success = true, language });
    }

    private static string NormalizeLanguage(string? language)
    {
        return language?.ToLowerInvariant() switch
        {
            "en" => "en",
            "zh" => "zh",
            _ => "vi"
        };
    }

    public sealed class SetLanguageRequest
    {
        public string? Language { get; init; }
    }
}
