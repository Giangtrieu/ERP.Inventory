namespace ERP.Inventory.Application.Common;

public sealed class CurrentUserContext
{
    public string UserId { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public string LanguageCode { get; init; } = "vi";
    public IReadOnlyCollection<string> Roles { get; init; } = Array.Empty<string>();
    public IReadOnlyCollection<int> WarehouseIds { get; init; } = Array.Empty<int>();
    public string AuthMode { get; init; } = string.Empty;

    public bool IsSuper => string.Equals(AuthMode, "Super", StringComparison.OrdinalIgnoreCase);
    public bool IsAdmin => IsSuper || Roles.Contains("Admin", StringComparer.OrdinalIgnoreCase);
    public bool CanManage => IsSuper || IsAdmin || Roles.Contains("Warehouse Manager", StringComparer.OrdinalIgnoreCase);
    public bool CanOperate => IsSuper || CanManage || Roles.Contains("Warehouse Staff", StringComparer.OrdinalIgnoreCase);

    public bool CanAccessWarehouse(int warehouseId)
    {
        return IsSuper || IsAdmin || WarehouseIds.Contains(warehouseId);
    }
}
