using ERP.Inventory.Domain.Common;
using ERP.Inventory.Domain.Enums;

namespace ERP.Inventory.Domain.Entities;

public class InboundDocument : DocumentBase
{
    public int? SourceExternalPartyId { get; set; }
    public ExternalParty? SourceExternalParty { get; set; }
    /// <summary>Người nhập kho — FK đến ExternalParty (type=Receiver)</summary>
    public int? ReceiverId { get; set; }
    public ExternalParty? Receiver { get; set; }
    public int WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public string PartyDepartment { get; set; } = string.Empty;
    public string PartyPhone { get; set; } = string.Empty;
    public string DepartmentOwner { get; set; } = string.Empty;
    public ICollection<InboundDocumentLine> Lines { get; set; } = new List<InboundDocumentLine>();
    public ICollection<InboundDocumentLog> Logs { get; set; } = new List<InboundDocumentLog>();
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
    public ICollection<RepairDocumentLine> Lines { get; set; } = new List<RepairDocumentLine>();
    public ICollection<RepairDocumentLog> Logs { get; set; } = new List<RepairDocumentLog>();
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
    public bool IsReturned { get; set; }
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
    public ICollection<BorrowDocumentLog> Logs { get; set; } = new List<BorrowDocumentLog>();
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
/// <summary>Log entry cho mỗi thao tác trên BorrowDocument: BorrowIssue hoặc BorrowReturn.</summary>
public class BorrowDocumentLog
{
    public int Id { get; set; }
    public int BorrowDocumentId { get; set; }
    public BorrowDocument? BorrowDocument { get; set; }
    public int ItemInstanceId { get; set; }
    public ItemInstance? ItemInstance { get; set; }
    /// <summary>BorrowIssue | BorrowReturn</summary>
    public string Action { get; set; } = string.Empty;
    public string OldStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    public string? Borrower { get; set; }
    public string? BorrowDepartment { get; set; } = string.Empty;
    public string? BorrowerPhone { get; set; } = string.Empty;
    public string? DepartmentOwner { get; set; } = string.Empty;
    public string? OldLocationText { get; set; }
    public string? NewLocationText { get; set; }
    public string PerformedBy { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? Note { get; set; }
}

/// <summary>Log entry cho mỗi ItemInstance được nhập kho qua InboundDocument.</summary>
public class InboundDocumentLog
{
    public int Id { get; set; }
    public int InboundDocumentId { get; set; }
    public InboundDocument? InboundDocument { get; set; }
    public int ItemInstanceId { get; set; }
    public ItemInstance? ItemInstance { get; set; }
    /// <summary>InboundReceive</summary>
    public string Action { get; set; } = "InboundReceive";
    public string OldStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    /// <summary>Người nhập kho: Mã-Tên</summary>
    public string? Receiver { get; set; }
    public string? ReceiverPhone { get; set; }
    public string? ReceiverDepartment { get; set; }
    public string? DepartmentOwner { get; set; }
    public string? OldLocationText { get; set; }
    public string? NewLocationText { get; set; }
    public string PerformedBy { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? Note { get; set; }
}

public class AdjustmentDocument : DocumentBase
{
    public int WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public string Reason { get; set; } = string.Empty;
    public ICollection<AdjustmentDocumentLine> Lines { get; set; } = new List<AdjustmentDocumentLine>();
    public ICollection<AdjustmentDocumentLog> Logs { get; set; } = new List<AdjustmentDocumentLog>();
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
    /// <summary>Draft | InProgress | Finalized — hỗ trợ kiểm kê từng phần (session-based).</summary>
    public string SessionStatus { get; set; } = "Draft";
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

/// <summary>Log entry cho mỗi thao tác trên RepairDocument: RepairSend | RepairReceive.</summary>
public class RepairDocumentLog
{
    public int Id { get; set; }
    public int RepairDocumentId { get; set; }
    public RepairDocument? RepairDocument { get; set; }
    public int ItemInstanceId { get; set; }
    public ItemInstance? ItemInstance { get; set; }
    /// <summary>RepairSend | RepairReceive</summary>
    public string Action { get; set; } = string.Empty;
    public string OldStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    /// <summary>Tên đơn vị sửa chữa — snapshot tại thời điểm thao tác.</summary>
    public string? RepairVendorName { get; set; }
    /// <summary>Địa điểm sửa chữa bên ngoài (external location text).</summary>
    public string? ExternalLocation { get; set; }
    public string? OldLocationText { get; set; }
    public string? NewLocationText { get; set; }
    public string? RepairResultNote { get; set; }
    public string PerformedBy { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? Note { get; set; }
}

/// <summary>Log entry cho mỗi thao tác trên AdjustmentDocument: Adjust | Replace-Out | Replace-In.</summary>
public class AdjustmentDocumentLog
{
    public int Id { get; set; }
    public int AdjustmentDocumentId { get; set; }
    public AdjustmentDocument? AdjustmentDocument { get; set; }
    public int ItemInstanceId { get; set; }
    public ItemInstance? ItemInstance { get; set; }
    /// <summary>Adjust | Replace-Out | Replace-In</summary>
    public string Action { get; set; } = "Adjust";
    public string OldStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    public string? OldLocationText { get; set; }
    public string? NewLocationText { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string PerformedBy { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? Note { get; set; }
}
