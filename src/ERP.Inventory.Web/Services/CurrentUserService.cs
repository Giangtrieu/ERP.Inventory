using ERP.Inventory.Application.Common;
using ERP.Inventory.Application.Interfaces;
using System.Security.Claims;

namespace ERP.Inventory.Web.Services;

public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public CurrentUserContext GetCurrentUser()
    {
        var principal = _httpContextAccessor.HttpContext?.User;
        var userId = principal?.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var name = principal?.Identity?.IsAuthenticated == true
            ? principal.Identity.Name ?? "authenticated-user"
            : string.Empty;

        var roles = principal?.FindAll(ClaimTypes.Role).Select(x => x.Value).ToArray() ?? Array.Empty<string>();
        var warehouseIds = principal?.FindAll("warehouse_id")
            .Select(x => int.TryParse(x.Value, out var id) ? id : 0)
            .Where(x => x > 0)
            .Distinct()
            .ToArray() ?? Array.Empty<int>();

        var language = principal?.FindFirstValue("language") ?? "vi";

        return new CurrentUserContext
        {
            UserId = userId,
            UserName = name,
            LanguageCode = language,
            Roles = roles,
            WarehouseIds = warehouseIds
        };
    }
}
