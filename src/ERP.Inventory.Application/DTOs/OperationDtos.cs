using ERP.Inventory.Domain.Enums;

namespace ERP.Inventory.Application.DTOs;

public sealed class PostedDocumentDto
{
    public string DocumentType { get; init; } = string.Empty;
    public int DocumentId { get; init; }
    public string DocumentNo { get; init; } = string.Empty;
    public DateTime PostedAt { get; init; }
}

public sealed class InboundRequest
{
    public int? SourceExternalPartyId { get; init; }
    public int WarehouseId { get; init; }
    public DateTime DocumentDate { get; init; } = DateTime.Now;
    public string? Note { get; init; }
    public IReadOnlyCollection<InboundLineRequest> Lines { get; init; } = Array.Empty<InboundLineRequest>();
}

public sealed class InboundLineRequest
{
    public int ItemId { get; init; }
    public string? SerialNumber { get; init; }
    public string? Barcode { get; init; }
    public decimal Quantity { get; init; } = 1;
    public int BinLocationId { get; init; }
    public string? Condition { get; init; }
    public string? Note { get; init; }
}

public sealed class MoveLocationRequest
{
    public int WarehouseId { get; init; }
    public DateTime DocumentDate { get; init; } = DateTime.Now;
    public string? Note { get; init; }
    public IReadOnlyCollection<MoveLocationLineRequest> Lines { get; init; } = Array.Empty<MoveLocationLineRequest>();
}

public sealed class MoveLocationLineRequest
{
    public int ItemInstanceId { get; init; }
    public int TargetBinLocationId { get; init; }
    public string? Note { get; init; }
}

public sealed class RepairSendRequest
{
    public int RepairVendorId { get; init; }
    public DateTime SendDate { get; init; } = DateTime.Now;
    public DateTime? ExpectedReturnDate { get; init; }
    public string Reason { get; init; } = string.Empty;
    public IReadOnlyCollection<int> ItemInstanceIds { get; init; } = Array.Empty<int>();
    public IReadOnlyCollection<RepairSendLineRequest> Lines { get; init; } = Array.Empty<RepairSendLineRequest>();
}

public sealed class RepairSendLineRequest
{
    public int ItemInstanceId { get; init; }
    public int? TargetBinLocationId { get; init; }
    public string? TargetExternalLocation { get; init; }
    public string? Note { get; init; }
}

public sealed class RepairReceiveRequest
{
    public int RepairDocumentId { get; init; }
    public RepairResult Result { get; init; }
    public int? TargetBinLocationId { get; init; }
    public string? ResultNote { get; init; }
    public IReadOnlyCollection<RepairReceiveLineRequest> Lines { get; init; } = Array.Empty<RepairReceiveLineRequest>();
}

public sealed class RepairReceiveLineRequest
{
    public int ItemInstanceId { get; init; }
    public int? TargetBinLocationId { get; init; }
    public string? NewSerialNumber { get; init; }
}

public sealed class BorrowLendRequest
{
    public string DocumentNo { get; init; } = string.Empty;
    public int WarehouseId { get; init; }
    public int BorrowerId { get; init; }
    public DateTime BorrowDate { get; init; } = DateTime.Now;
    public DateTime DueDate { get; init; }
    public string Purpose { get; init; } = string.Empty;
    public string BorrowDepartment { get; init; } = string.Empty;
    public string ApprovedBy { get; init; } = string.Empty;
    public string BorrowerPhone { get; init; } = string.Empty;
    public string DepartmentOwner { get; init; } = string.Empty;
    public IReadOnlyCollection<int> ItemInstanceIds { get; init; } = Array.Empty<int>();
    public IReadOnlyCollection<BorrowLendLineRequest> Lines { get; init; } = Array.Empty<BorrowLendLineRequest>();
}

public sealed class BorrowLendLineRequest
{
    public int ItemInstanceId { get; init; }
    public int? TargetBinLocationId { get; init; }
    public string? TargetExternalLocation { get; init; }
    public string? Note { get; init; }
}

public sealed class BorrowReturnRequest
{
    public int BorrowDocumentId { get; init; }
    public DateTime ReturnDate { get; init; } = DateTime.Now;
    public IReadOnlyCollection<BorrowReturnLineRequest> Lines { get; init; } = Array.Empty<BorrowReturnLineRequest>();
}

public sealed class BorrowReturnLineRequest
{
    public int ItemInstanceId { get; init; }
    public BorrowReturnCondition Condition { get; init; }
    public int? TargetBinLocationId { get; init; }
    public string? Note { get; init; }
}

public sealed class AdjustmentRequest
{
    public int WarehouseId { get; init; }
    public DateTime DocumentDate { get; init; } = DateTime.Now;
    public string Reason { get; init; } = string.Empty;
    public IReadOnlyCollection<AdjustmentLineRequest> Lines { get; init; } = Array.Empty<AdjustmentLineRequest>();
}

public sealed class AdjustmentLineRequest
{
    public int ItemInstanceId { get; init; }
    public ItemStatus NewStatus { get; init; }
    public int? TargetBinLocationId { get; init; }
    public int? TargetExternalPartyId { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public sealed class InventoryCheckRequest
{
    public int WarehouseId { get; init; }
    public DateTime SessionDate { get; init; } = DateTime.Now;
    public string CountMethod { get; init; } = "Scan";
    public string ResponsibleStaff { get; init; } = string.Empty;
    public IReadOnlyCollection<InventoryCheckLineRequest> Lines { get; init; } = Array.Empty<InventoryCheckLineRequest>();
}

public sealed class InventoryCheckLineRequest
{
    public int? ItemInstanceId { get; init; }
    public int? ActualBinLocationId { get; init; }
    public InventoryCheckLineResult Result { get; init; }
    public string? Note { get; init; }
}
