namespace ERP.Inventory.Domain.Enums;

public enum MovementActionType
{
    Inbound = 1,
    MoveLocation = 2,
    SendToRepair = 3,
    ReceiveFromRepair = 4,
    Lend = 5,
    ReturnBorrowed = 6,
    Adjustment = 7,
    InventoryCheck = 8,
    ImportOpening = 9,
    Dispose = 10,
    Transfer = 11
}

