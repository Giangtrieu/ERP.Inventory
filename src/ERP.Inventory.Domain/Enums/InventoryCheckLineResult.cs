namespace ERP.Inventory.Domain.Enums;

public enum InventoryCheckLineResult
{
    Matched = 1,
    Missing = 2,
    Extra = 3,
    WrongLocation = 4,
    Damaged = 5
}

