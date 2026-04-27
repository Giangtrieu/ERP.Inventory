namespace ERP.Inventory.Domain.Enums;

public enum InventoryTransactionType
{
    Inbound = 1,
    Move = 2,
    RepairSend = 3,
    RepairReceive = 4,
    BorrowLend = 5,
    BorrowReturn = 6,
    Adjustment = 7,
    InventoryCheck = 8,
    OpeningBalance = 9
}

