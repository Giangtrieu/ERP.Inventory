using ERP.Inventory.Domain.Common;
using ERP.Inventory.Domain.Enums;

namespace ERP.Inventory.Domain.Entities;

public class InboundDocument : DocumentBase
{
    public int? SourceExternalPartyId { get; set; }
    public ExternalParty? SourceExternalParty { get; set; }
    public int WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public ICollection<InboundDocumentLine> Lines { get; set; } = new List<InboundDocumentLine>();
}

public class InboundDocumentLine : AuditableEntity
{
    public int InboundDocumentId { get; set; }
    public InboundDocument? InboundDocument { get; set; }
    public int ItemId { get; set; }
    public Item? Item { get; set; }
    public int? ItemInstanceId { get; set; }
    public ItemInstance? ItemInstance { get; set; }
    public string? SerialNumber { get; set; }
    public string? Barcode { get; set; }
    public decimal Quantity { get; set; } = 1;
    public int BinLocationId { get; set; }
    public BinLocation? BinLocation { get; set; }
    public string? Condition { get; set; }
    public string? Note { get; set; }
}

public class MoveDocument : DocumentBase
{
    public int WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public ICollection<MoveDocumentLine> Lines { get; set; } = new List<MoveDocumentLine>();
}

public class MoveDocumentLine : AuditableEntity
{
    public int MoveDocumentId { get; set; }
    public MoveDocument? MoveDocument { get; set; }
    public int ItemInstanceId { get; set; }
    public ItemInstance? ItemInstance { get; set; }
    public int? FromBinLocationId { get; set; }
    public BinLocation? FromBinLocation { get; set; }
    public int TargetBinLocationId { get; set; }
    public BinLocation? TargetBinLocation { get; set; }
    public string? Note { get; set; }
}

public class RepairDocument : DocumentBase
{
    public int RepairVendorId { get; set; }
    public ExternalParty? RepairVendor { get; set; }
    public DateTime? ExpectedReturnDate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public RepairResult? ReceiveResult { get; set; }
    public ICollection<RepairDocumentLine> Lines { get; set; } = new List<RepairDocumentLine>();
}

public class RepairDocumentLine : AuditableEntity
{
    public int RepairDocumentId { get; set; }
    public RepairDocument? RepairDocument { get; set; }
    public int ItemInstanceId { get; set; }
    public ItemInstance? ItemInstance { get; set; }
    public int? FromBinLocationId { get; set; }
    public BinLocation? FromBinLocation { get; set; }
    public int? TargetBinLocationId { get; set; }
    public BinLocation? TargetBinLocation { get; set; }
    public string? TargetExternalLocation { get; set; }
    public string? NewSerialNumber { get; set; }
    public string? RepairResultNote { get; set; }
}

public class BorrowDocument : DocumentBase
{
    public int BorrowerId { get; set; }
    public ExternalParty? Borrower { get; set; }
    public DateTime DueDate { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public string BorrowDepartment { get; set; } = string.Empty;
    public string BorrowerPhone { get; set; } = string.Empty;
    public string DepartmentOwner { get; set; } = string.Empty;
    public ICollection<BorrowDocumentLine> Lines { get; set; } = new List<BorrowDocumentLine>();
}

public class BorrowDocumentLine : AuditableEntity
{
    public int BorrowDocumentId { get; set; }
    public BorrowDocument? BorrowDocument { get; set; }
    public int ItemInstanceId { get; set; }
    public ItemInstance? ItemInstance { get; set; }
    public int? FromBinLocationId { get; set; }
    public BinLocation? FromBinLocation { get; set; }
    public int? TargetBinLocationId { get; set; }
    public BinLocation? TargetBinLocation { get; set; }
    public string? TargetExternalLocation { get; set; }
    public bool IsReturned { get; set; }
    public BorrowReturnCondition? ReturnCondition { get; set; }
    public DateTime? ReturnedAt { get; set; }
    public string? Note { get; set; }
}

public class AdjustmentDocument : DocumentBase
{
    public int WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public string Reason { get; set; } = string.Empty;
    public ICollection<AdjustmentDocumentLine> Lines { get; set; } = new List<AdjustmentDocumentLine>();
}

public class AdjustmentDocumentLine : AuditableEntity
{
    public int AdjustmentDocumentId { get; set; }
    public AdjustmentDocument? AdjustmentDocument { get; set; }
    public int ItemInstanceId { get; set; }
    public ItemInstance? ItemInstance { get; set; }
    public ItemStatus OldStatus { get; set; }
    public ItemStatus NewStatus { get; set; }
    public int? FromBinLocationId { get; set; }
    public int? TargetBinLocationId { get; set; }
    public int? TargetExternalPartyId { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class InventoryCheckDocument : DocumentBase
{
    public int WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public string CountMethod { get; set; } = "Scan";
    public string ResponsibleStaff { get; set; } = string.Empty;
    public ICollection<InventoryCheckLine> Lines { get; set; } = new List<InventoryCheckLine>();
}

public class InventoryCheckLine : AuditableEntity
{
    public int InventoryCheckDocumentId { get; set; }
    public InventoryCheckDocument? InventoryCheckDocument { get; set; }
    public int? ItemInstanceId { get; set; }
    public ItemInstance? ItemInstance { get; set; }
    public int? SystemBinLocationId { get; set; }
    public int? ActualBinLocationId { get; set; }
    public InventoryCheckLineResult Result { get; set; }
    public string? Note { get; set; }
}
