using ERP.Inventory.Domain.Enums;

namespace ERP.Inventory.Application.DTOs;

public sealed class PostedDocumentDto
{
    public string DocumentType { get; init; } = string.Empty;
    public int DocumentId { get; init; }
    public string DocumentNo { get; init; } = string.Empty;
    public DateTime PostedAt { get; init; }
}

// ─── Inbound ────────────────────────────────────────────────
public sealed class InboundRequest
{
    /// <summary>Text code of supplier (optional). Backend resolves to ExternalParty Id.</summary>
    public string? SourcePartyer { get; init; } = string.Empty;
    public string SourcePartyCode { get; set; } = string.Empty;
    public string SourcePartyName { get; set; } = string.Empty;
    /// <summary>Người nhập kho dạng "Mã-Tên". Backend resolve/create ExternalParty(Receiver).</summary>
    public string Receiver { get; init; } = string.Empty;
    public string ReceiverCode { get; set; } = string.Empty;
    public string ReceiverName { get; set; } = string.Empty;
    public string ReceiverPhone { get; init; } = string.Empty;
    public string ReceiverDepartment { get; init; } = string.Empty;
    public string DepartmentOwner { get; init; } = string.Empty;
    public string ApprovedBy { get; init; } = string.Empty;
    public string DocumentNo { get; init; } = string.Empty;
    public string WarehouseCode { get; init; } = string.Empty;
    public int WarehouseId { get; init; }
    public DateTime DocumentDate { get; init; } = DateTime.UtcNow;
    public string? Note { get; init; }
    /// <summary>Chủ sở hữu hàng — áp dụng cho tất cả ItemInstance trong phiếu nhập này.</summary>
    public string? OwnerName { get; init; }
    public IReadOnlyCollection<InboundLineRequest> Lines { get; init; } = Array.Empty<InboundLineRequest>();
}


public sealed class InboundLineRequest
{
    /// <summary>Item category code — used with SerialNumber to locate the item.</summary>
    public string ItemCode { get; init; } = string.Empty;
    public string? SerialNumber { get; init; }
    // Barcode is auto-set = SerialNumber in backend — not exposed on UI
    public string? MT { get; init; }
    public decimal Quantity { get; init; } = 1;
    /// <summary>BinLocation code (unique). Backend resolves to BinLocation Id.</summary>
    public string BinCode { get; init; } = string.Empty;
    public string? Condition { get; init; }
    public string? Note { get; init; }
}

// ─── Move ───────────────────────────────────────────────────
public sealed class MoveLocationRequest
{
    public int WarehouseId { get; init; }
    public string WarehouseCode { get; init; } = string.Empty;
    public DateTime DocumentDate { get; init; } = DateTime.UtcNow;
    public string? Note { get; init; }
    public IReadOnlyCollection<MoveLocationLineRequest> Lines { get; init; } = Array.Empty<MoveLocationLineRequest>();
}

public sealed class MoveLocationLineRequest
{
    /// <summary>Item type code (ItemCode) to identify the item type.</summary>
    public string ItemCode { get; init; } = string.Empty;
    /// <summary>Serial number of the specific item instance.</summary>
    public string SerialNumber { get; init; } = string.Empty;
    /// <summary>Target bin code (unique).</summary>
    public string TargetBinCode { get; init; } = string.Empty;
    public string? Note { get; init; }
}

// ─── Repair ─────────────────────────────────────────────────
public sealed class RepairSendRequest
{
    /// <summary>
    /// Optional: if provided, backend will find or create a repair document with this number.
    /// Supports multi-batch append — same behavior as BorrowLend.
    /// </summary>
    public string DocumentNo { get; init; } = string.Empty;
    /// <summary>Text code for repair vendor. Backend resolves to ExternalParty Id.</summary>
    public string RepairVendorCode { get; init; } = string.Empty;
    public DateTime SendDate { get; init; } = DateTime.UtcNow;
    public DateTime? ExpectedReturnDate { get; init; }
    public string Reason { get; init; } = string.Empty;
    public IReadOnlyCollection<RepairSendLineRequest> Lines { get; init; } = Array.Empty<RepairSendLineRequest>();
}

public sealed class RepairSendLineRequest
{
    public string ItemCode { get; init; } = string.Empty;
    public string SerialNumber { get; init; } = string.Empty;
    /// <summary>Text description of external repair location.</summary>
    public string? TargetExternalLocation { get; init; }
    public string? Note { get; init; }
}

public sealed class RepairReceiveRequest
{
    /// <summary>Internal document ID (existing). Used when FE selects from document list.</summary>
    public int RepairDocumentId { get; init; }
    /// <summary>Alternative: look up by document number string instead of ID.</summary>
    public string RepairDocumentNo { get; init; } = string.Empty;
    /// <summary>Header-level target warehouse (used by import flow).</summary>
    public int? TargetWarehouseId { get; init; }
    /// <summary>Header-level target bin code (used by import flow).</summary>
    public string? TargetBinCode { get; init; }
    public string? ResultNote { get; init; }
    public string? Note { get; init; }
    /// <summary>Each line has its own repair result and target bin.</summary>
    public IReadOnlyCollection<RepairReceiveLineRequest> Lines { get; init; } = Array.Empty<RepairReceiveLineRequest>();
}

public sealed class RepairReceiveLineRequest
{
    public string ItemCode { get; init; } = string.Empty;
    public string SerialNumber { get; init; } = string.Empty;
    /// <summary>Per-line repair result: Success / Replaced / Failed.</summary>
    public RepairResult Result { get; init; }
    /// <summary>Target bin code for returning item.</summary>
    public string TargetBinCode { get; init; } = string.Empty;
    public string? NewSerialNumber { get; init; }
    public string? Note { get; init; }
}


// ─── Borrow ─────────────────────────────────────────────────
public sealed class BorrowLendRequest
{
    public string DocumentNo { get; init; } = string.Empty;
    public int WarehouseId { get; init; } 
    public string WarehouseCode { get; init; } = string.Empty;
    /// <summary>Text code for borrower. Backend resolves to ExternalParty Id.</summary>
    public string Borrower { get; init; } = string.Empty;
    public string BorrowerCode { get; set; } = string.Empty;
    public string BorrowerName { get; set; } = string.Empty;
    public DateTime BorrowDate { get; init; } = DateTime.UtcNow;
    public DateTime DueDate { get; init; }
    public string Purpose { get; init; } = string.Empty;
    public string BorrowDepartment { get; init; } = string.Empty;
    public string ApprovedBy { get; init; } = string.Empty;
    public string BorrowerPhone { get; init; } = string.Empty;
    public string DepartmentOwner { get; init; } = string.Empty;
    public IReadOnlyCollection<BorrowLendLineRequest> Lines { get; init; } = Array.Empty<BorrowLendLineRequest>();
}

public sealed class BorrowLendLineRequest
{
    public string ItemCode { get; init; } = string.Empty;
    public string SerialNumber { get; init; } = string.Empty;
    public string? TargetExternalLocation { get; init; }
    public string? Note { get; init; }
}

public sealed class BorrowReturnRequest
{
    public int BorrowDocumentId { get; init; }
    /// <summary>Alternative string lookup for import (find by DocumentNo).</summary>

    public string ReturnerCode { get; set; } = string.Empty;
    public string ReturnerName { get; set; } = string.Empty;
    public string Returner { get; init; } = string.Empty;
    public string Purpose { get; init; } = string.Empty;
    public string BorrowDepartment { get; init; } = string.Empty;
    public string ApprovedBy { get; init; } = string.Empty;
    public string BorrowerPhone { get; init; } = string.Empty;
    public string DepartmentOwner { get; init; } = string.Empty;
    public string? BorrowDocumentNo { get; init; }
    public DateTime ReturnDate { get; init; } = DateTime.UtcNow;
    public string? ReturnLocationBinCode { get; init; }
    public string? Note { get; init; }
    public IReadOnlyCollection<BorrowReturnLineRequest> Lines { get; init; } = Array.Empty<BorrowReturnLineRequest>();
}

public sealed class BorrowReturnLineRequest
{
    public string ItemCode { get; init; } = string.Empty;
    public string SerialNumber { get; init; } = string.Empty;
    public BorrowReturnCondition Condition { get; init; }
    /// <summary>Target bin code for returning item.</summary>
    public string? TargetBinCode { get; init; }
    public string? Note { get; init; }
}

// ─── Adjustment ─────────────────────────────────────────────
public sealed class AdjustmentRequest
{
    public int WarehouseId { get; init; }
    public string WarehouseCode { get; init; } = string.Empty;
    public DateTime DocumentDate { get; init; } = DateTime.UtcNow;
    public string Reason { get; init; } = string.Empty;
    public IReadOnlyCollection<AdjustmentLineRequest> Lines { get; init; } = Array.Empty<AdjustmentLineRequest>();
}

public sealed class AdjustmentLineRequest
{
    public string ItemCode { get; init; } = string.Empty;
    public string SerialNumber { get; init; } = string.Empty;
    public string NewSerialNumber { get; init; } = string.Empty;
    public ItemStatus NewStatus { get; init; }
    public string? TargetBinCode { get; init; }
    public string? TargetExternalPartyCode { get; init; }
    public string Reason { get; init; } = string.Empty;
}

// ─── Inventory Check — Session-based ────────────────────────
/// <summary>Tạo phiên kiểm kê mới (InProgress). Lines sẽ được gửi qua ScanBatch sau.</summary>
public sealed class InventoryCheckSessionRequest
{
    public int WarehouseId { get; init; }
    public string WarehouseCode { get; init; } = string.Empty;
    public DateTime SessionDate { get; init; } = DateTime.UtcNow;
    public DocumentPeriodType DocumentPeriodType { get; init; }
    public string CountMethod { get; init; } = "Scan";
    public string ResponsibleStaff { get; init; } = string.Empty;
    public string? Note { get; init; }
}

/// <summary>Gửi một batch scan vào phiên kiểm kê đang InProgress.</summary>
public sealed class InventoryCheckScanRequest
{
    public int DocumentId { get; init; }
    /// <summary>Lines scanned từ UI: ItemCode + SerialNumber + BinCode.
    /// Backend tự xác định Matched / WrongLocation / Extra.</summary>
    public IReadOnlyCollection<InventoryCheckLineRequest> Lines { get; init; } = Array.Empty<InventoryCheckLineRequest>();
}

public sealed class InventoryCheckLineRequest
{
    public string ItemCode { get; init; } = string.Empty;
    public string SerialNumber { get; init; } = string.Empty;
    /// <summary>Bin code thực tế tìm thấy khi kiểm kê.</summary>
    public string BinCode { get; init; } = string.Empty;
    public string? Note { get; init; }
}

/// <summary>Kết quả trả về sau mỗi ScanBatch.</summary>
public sealed class ScanBatchResultDto
{
    public int DocumentId { get; init; }
    public string DocumentNo { get; init; } = string.Empty;
    public int BatchMatched { get; init; }
    public int BatchWrongLocation { get; init; }
    public int BatchExtra { get; init; }
    public int BatchSkipped { get; init; }
    /// <summary>InProgress | Finalized</summary>
    public string SessionStatus { get; init; } = string.Empty;
}

// Quantity inventory - for non-bin-tracked items managed by ItemCode + SN + quantity.
public sealed class QuantityInventoryRequest
{
    public string DocumentNo { get; init; } = string.Empty;
    public int WarehouseId { get; init; }
    public DateTime DocumentDate { get; init; } = DateTime.UtcNow;
    public string ItemCategoryCode { get; init; } = string.Empty;
    public string ItemCode { get; init; } = string.Empty;
    public string ApprovedBy { get; init; } = string.Empty;
    public string? Note { get; init; }
    /// <summary>Chủ sở hữu hàng — áp dụng cho tất cả ItemInstance trong operation này.</summary>
    public string? OwnerName { get; init; }
    public IReadOnlyCollection<QuantityInventoryLineRequest> Lines { get; init; } = Array.Empty<QuantityInventoryLineRequest>();
}

public sealed class QuantityInventoryLineRequest
{
    public string SnCode { get; init; } = string.Empty;
    public ItemStatus Status { get; init; } = ItemStatus.Normal;
    public decimal Quantity { get; init; }
    public string? Note { get; init; }
}

public sealed class QuantityStockBalanceDto
{
    public int Id { get; init; }
    public int WarehouseId { get; init; }
    public string WarehouseCode { get; init; } = string.Empty;
    public int ItemId { get; init; }
    public string ItemCode { get; init; } = string.Empty;
    public string ItemName { get; init; } = string.Empty;
    public string SnCode { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public string? OwnerName { get; init; }
    public DateTime? LastUpdatedAt { get; init; }
}

public sealed class QuantityInventoryTransactionDto
{
    public long Id { get; init; }
    public string TransactionType { get; init; } = string.Empty;
    public string DocumentNo { get; init; } = string.Empty;
    public DateTime PostedAt { get; init; }
    public string ItemCode { get; init; } = string.Empty;
    public string SnCode { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public decimal QuantityDelta { get; init; }
    public string PostedBy { get; init; } = string.Empty;
}

public sealed class QuantityInstanceDto
{
    public int Id { get; init; }
    public string SnCode { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string TrackingType { get; init; } = string.Empty;
    public string WarehouseCode { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public string? OwnerName { get; init; }
    public DateTime CreatedAt { get; init; }
}

