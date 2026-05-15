namespace ERP.Inventory.Domain.Enums;

public enum ItemStatus
{
    /// <summary>In warehouse – normal condition (primary in-warehouse status).</summary>
    Normal = 0,
    /// <summary>Legacy alias for Normal; kept for backward DB compat. New items use Normal.</summary>
    InStock = 1,
    Reserved = 2,
    Repairing = 3,
    LentOut = 4,
    Returned = 5,
    /// <summary>In warehouse – damaged condition.</summary>
    Damaged = 6,
    Lost = 7,
    Disposed = 8,
    InTransit = 9,
    Replacement = 10,
    /// <summary>In warehouse – condemned/scrapped condition.</summary>
    Scrapped = 11,
}

/// <summary>
/// Status values shown in the inventory list filter UI.
/// Normal = 0 means "normal condition in warehouse".
/// Removed InStock(1) – Normal(0) is canonical now.
/// </summary>
public enum ItemStatusView
{
    Normal = 0,
    InStock = 1,
    Repairing = 3,
    LentOut = 4,
    Damaged = 6,
    Lost = 7,
    Disposed = 8,
    Replacement = 10,
    Scrapped = 11,
}

/// <summary>
/// Used for the "in-warehouse" inventory filter dropdown.
/// InStock(1) acts as a group filter: Normal + Damaged + Scrapped.
/// Individual sub-statuses can also be selected.
/// </summary>
public enum InventoryStatus
{
    /// <summary>Group filter: all in-warehouse items (Normal + Damaged + Scrapped).</summary>
    InStock = 1,
    /// <summary>Normal condition items only.</summary>
    Normal = 0,
    /// <summary>Damaged items only.</summary>
    Damaged = 6,
    /// <summary>Scrapped/condemned items only.</summary>
    Scrapped = 11,
    Replacement = 10,
}
