namespace ERP.Inventory.Domain.Enums;

public enum ItemStatus
{
    InStock = 1,
    Reserved = 2,
    Repairing = 3,
    LentOut = 4,
    Returned = 5,
    Damaged = 6,
    Lost = 7,
    Disposed = 8,
    InTransit = 9
}

