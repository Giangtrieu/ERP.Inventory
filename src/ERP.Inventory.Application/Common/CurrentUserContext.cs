namespace ERP.Inventory.Application.Common;

public sealed class CurrentUserContext
{
    public string UserId { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public string LanguageCode { get; init; } = "vi";
    public IReadOnlyCollection<string> Roles { get; init; } = Array.Empty<string>();
    public IReadOnlyCollection<int> WarehouseIds { get; init; } = Array.Empty<int>();

    public bool IsAdmin => Roles.Contains("Admin", StringComparer.OrdinalIgnoreCase);
    public bool CanManage => IsAdmin || Roles.Contains("Warehouse Manager", StringComparer.OrdinalIgnoreCase);
    public bool CanOperate => CanManage || Roles.Contains("Warehouse Staff", StringComparer.OrdinalIgnoreCase);

    public bool CanAccessWarehouse(int warehouseId)
    {
        return IsAdmin || WarehouseIds.Contains(warehouseId);
    }
}
