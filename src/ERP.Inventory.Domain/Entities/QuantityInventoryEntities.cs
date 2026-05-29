using ERP.Inventory.Domain.Common;
using ERP.Inventory.Domain.Enums;

namespace ERP.Inventory.Domain.Entities;

public class QuantityStockBalance : AuditableEntity
{
    public int WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public int ItemId { get; set; }
    public Item? Item { get; set; }
    public string SnCode { get; set; } = string.Empty;
    public ItemStatus Status { get; set; } = ItemStatus.Normal;
    public decimal Quantity { get; set; }
}

public class QuantityInventoryDocument : AuditableEntity
{
    public string DocumentNo { get; set; } = string.Empty;
    public DateTime DocumentDate { get; set; } = DateTime.UtcNow;
    public DateTime PostedAt { get; set; } = DateTime.UtcNow;
    public QuantityInventoryDocumentType DocumentType { get; set; }
    public int WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public string OperatorUserId { get; set; } = string.Empty;
    public string OperatorUserCode { get; set; } = string.Empty;
    public string OperatorUserName { get; set; } = string.Empty;
    public string SenderCode { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public string SenderPhone { get; set; } = string.Empty;
    public string ReceiverCode { get; set; } = string.Empty;
    public string ReceiverName { get; set; } = string.Empty;
    public string ReceiverPhone { get; set; } = string.Empty;
    public string ApprovedBy { get; set; } = string.Empty;
    public string? Note { get; set; }
    public ICollection<QuantityInventoryDocumentLine> Lines { get; set; } = new List<QuantityInventoryDocumentLine>();
}

public class QuantityInventoryDocumentLine : AuditableEntity
{
    public int QuantityInventoryDocumentId { get; set; }
    public QuantityInventoryDocument? QuantityInventoryDocument { get; set; }
    public int ItemId { get; set; }
    public Item? Item { get; set; }
    public string SnCode { get; set; } = string.Empty;
    public ItemStatus Status { get; set; } = ItemStatus.Normal;
    public decimal Quantity { get; set; }
    public Guid? LifecycleBatchId { get; set; }
    public string? Note { get; set; }
}

public class QuantityInventoryTransaction
{
    public long Id { get; set; }
    public QuantityInventoryDocumentType TransactionType { get; set; }
    public int WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public int ItemId { get; set; }
    public Item? Item { get; set; }
    public string SnCode { get; set; } = string.Empty;
    public ItemStatus StatusAfter { get; set; } = ItemStatus.Normal;
    public decimal QuantityDelta { get; set; }
    public int DocumentId { get; set; }
    public string DocumentNo { get; set; } = string.Empty;
    public Guid? LifecycleBatchId { get; set; }
    public DateTime PostedAt { get; set; } = DateTime.UtcNow;
    public string PostedBy { get; set; } = string.Empty;
}

