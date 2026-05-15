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
    /// <summary>
    /// Returns true when the item is physically located inside a warehouse bin
    /// (normal, damaged or scrapped condition).
    /// </summary>
    public static bool IsInWarehouse(ItemStatus status) =>
        status is ItemStatus.Normal or ItemStatus.InStock  // InStock kept for legacy records
                or ItemStatus.Damaged or ItemStatus.Scrapped;

    /// <summary>Items that can be moved to another bin location.</summary>
    public bool CanMove(ItemStatus status) => IsInWarehouse(status);

    public bool CanSendToRepair(ItemStatus status)
    {
        return status is ItemStatus.Normal or ItemStatus.InStock or ItemStatus.Damaged;
    }

    /// <summary>Only normal-condition in-warehouse items may be lent out.</summary>
    public bool CanLend(ItemStatus status) =>
        status is ItemStatus.Normal or ItemStatus.InStock;

    public ItemStatus StatusAfterRepairReceive(RepairResult result)
    {
        return result switch
        {
            RepairResult.Success  => ItemStatus.Normal,
            RepairResult.Replaced => ItemStatus.Normal,
            RepairResult.Failed   => ItemStatus.Damaged,
            _                     => ItemStatus.Normal
        };
    }

    public ItemStatus StatusAfterBorrowReturn(BorrowReturnCondition condition)
    {
        return condition switch
        {
            BorrowReturnCondition.Normal   => ItemStatus.Normal,
            BorrowReturnCondition.Damaged  => ItemStatus.Damaged,
            BorrowReturnCondition.Lost     => ItemStatus.Lost,
            BorrowReturnCondition.Scrapped => ItemStatus.Scrapped,
            _                              => ItemStatus.Damaged
        };
    }
}
