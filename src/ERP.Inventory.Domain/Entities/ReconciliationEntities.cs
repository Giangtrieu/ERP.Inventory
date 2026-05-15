using ERP.Inventory.Domain.Common;
using ERP.Inventory.Domain.Enums;

namespace ERP.Inventory.Domain.Entities;

/// <summary>
/// Header của một danh sách tài sản tham chiếu.
/// Scoped theo Warehouse — mỗi list gắn với 1 kho cụ thể.
/// </summary>
public class ReferenceListHeader : AuditableEntity
{
    /// <summary>Mã danh sách — unique toàn hệ thống (e.g. "REF-WH01-2026").</summary>
    public string ListCode { get; set; } = string.Empty;

    /// <summary>Tên mô tả của danh sách.</summary>
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Kho áp dụng — bắt buộc.</summary>
    public int WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<ReferenceListItem> Items { get; set; } = new List<ReferenceListItem>();
    public ICollection<ReconciliationSession> Sessions { get; set; } = new List<ReconciliationSession>();
}

/// <summary>
/// Một dòng item trong danh sách tham chiếu.
/// Unique key: (ReferenceListId, ItemCode, SerialNumber).
/// </summary>
public class ReferenceListItem : AuditableEntity
{
    public int ReferenceListId { get; set; }
    public ReferenceListHeader? ReferenceList { get; set; }

    // --- Input từ Excel ---
    public string ItemCode { get; set; } = string.Empty;
    public string? SerialNumber { get; set; }

    // --- Resolved từ ERP tại thời điểm import (snapshot, nullable) ---
    public int? ItemInstanceId { get; set; }
    public ItemInstance? ItemInstance { get; set; }
    public int? ItemId { get; set; }
    public Item? Item { get; set; }
    public string? ResolvedItemName { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
    public string ImportedBy { get; set; } = string.Empty;
    public DateTime? LastUpdatedAt { get; set; }
    public string? Note { get; set; }
}

/// <summary>
/// Phiên đối soát — mỗi lần chạy so sánh ref list vs ERP tạo ra 1 session.
/// Kết quả là immutable snapshot tại thời điểm chạy.
/// </summary>
public class ReconciliationSession : AuditableEntity
{
    /// <summary>Số phiên — auto-generated: REC-20260514-001.</summary>
    public string SessionNo { get; set; } = string.Empty;

    public int ReferenceListId { get; set; }
    public ReferenceListHeader? ReferenceList { get; set; }

    /// <summary>Kho áp dụng đối soát — snapshot từ ReferenceList.WarehouseId.</summary>
    public int WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }

    /// <summary>Draft | Running | Completed | Archived</summary>
    public string SessionStatus { get; set; } = nameof(ReconciliationSessionStatus.Draft);

    public DateTime? RunAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    // --- Cached summary counters ---
    public int TotalRef { get; set; }
    public int TotalErp { get; set; }
    public int MatchedCount { get; set; }
    public int ERPOnlyCount { get; set; }
    public int RefOnlyCount { get; set; }

    public string? Note { get; set; }

    public ICollection<ReconciliationResult> Results { get; set; } = new List<ReconciliationResult>();
}

/// <summary>
/// Kết quả chi tiết từng dòng trong phiên đối soát.
/// Immutable sau khi session Completed.
/// </summary>
public class ReconciliationResult
{
    public int Id { get; set; }

    public int SessionId { get; set; }
    public ReconciliationSession? Session { get; set; }

    // --- Item info (snapshot tại thời điểm chạy) ---
    public string ItemCode { get; set; } = string.Empty;
    public string? SerialNumber { get; set; }
    public int? ItemInstanceId { get; set; }
    public ItemInstance? ItemInstance { get; set; }
    public string? ResolvedItemName { get; set; }

    // --- Result: Matched | ERPOnly | RefOnly ---
    public ReconciliationResultType ResultType { get; set; }

    // --- ERP snapshot tại thời điểm chạy ---
    public string? ErpStatus { get; set; }
    public string? ErpLocationText { get; set; }

    /// <summary>
    /// Chú thích bổ sung, ví dụ: "Đang mượn" khi item có Status=LentOut nhưng vẫn Matched.
    /// </summary>
    public string? Note { get; set; }

    /// <summary>FK về ReferenceListItem nguồn (nếu là Matched hoặc RefOnly).</summary>
    public int? RefListItemId { get; set; }
    public ReferenceListItem? RefListItem { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
