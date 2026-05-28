using ERP.Inventory.Domain.Common;
using ERP.Inventory.Domain.Enums;

namespace ERP.Inventory.Domain.Entities;

public class StockBalance : AuditableEntity
{
    public int WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public int? BinLocationId { get; set; }
    public BinLocation? BinLocation { get; set; }
    public int ItemId { get; set; }
    public Item? Item { get; set; }
    public ItemStatus Status { get; set; } = ItemStatus.Normal;
    public decimal Quantity { get; set; }
}

public class InventoryTransaction
{
    public long Id { get; set; }
    public InventoryTransactionType TransactionType { get; set; }
    public int ItemId { get; set; }
    public Item? Item { get; set; }
    public int? ItemInstanceId { get; set; }
    public ItemInstance? ItemInstance { get; set; }
    public int? WarehouseId { get; set; }
    public int? BinLocationId { get; set; }
    public decimal QuantityDelta { get; set; }
    public ItemStatus StatusAfter { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public int DocumentId { get; set; }
    public string DocumentNo { get; set; } = string.Empty;
    public DateTime PostedAt { get; set; } = DateTime.UtcNow;
    public string PostedBy { get; set; } = string.Empty;
}

