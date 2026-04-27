using ERP.Inventory.Domain.Enums;

namespace ERP.Inventory.Application.Services;

public interface IInventoryStatePolicy
{
    bool CanMove(ItemStatus status);
    bool CanSendToRepair(ItemStatus status);
    bool CanLend(ItemStatus status);
    ItemStatus StatusAfterRepairReceive(RepairResult result);
    ItemStatus StatusAfterBorrowReturn(BorrowReturnCondition condition);
}

public sealed class InventoryStatePolicy : IInventoryStatePolicy
{
    public bool CanMove(ItemStatus status) => status == ItemStatus.InStock;

    public bool CanSendToRepair(ItemStatus status)
    {
        return status is ItemStatus.InStock or ItemStatus.Damaged;
    }

    public bool CanLend(ItemStatus status) => status == ItemStatus.InStock;

    public ItemStatus StatusAfterRepairReceive(RepairResult result)
    {
        return result switch
        {
            RepairResult.Success => ItemStatus.InStock,
            RepairResult.Replaced => ItemStatus.InStock,
            RepairResult.Failed => ItemStatus.Damaged,
            _ => ItemStatus.Damaged
        };
    }

    public ItemStatus StatusAfterBorrowReturn(BorrowReturnCondition condition)
    {
        return condition switch
        {
            BorrowReturnCondition.Normal => ItemStatus.InStock,
            BorrowReturnCondition.Damaged => ItemStatus.Damaged,
            BorrowReturnCondition.Lost => ItemStatus.Lost,
            _ => ItemStatus.Damaged
        };
    }
}
