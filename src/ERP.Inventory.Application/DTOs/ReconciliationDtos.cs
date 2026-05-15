using ERP.Inventory.Domain.Enums;

namespace ERP.Inventory.Application.DTOs;

// ─── Reference List ───────────────────────────────────────────────────────────

/// <summary>Request tạo mới Reference List.</summary>
public class CreateReferenceListRequest
{
    public string ListCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int WarehouseId { get; set; }
}

/// <summary>
/// Request import Excel vào Reference List.
/// ImportMode: Supplement (upsert) hoặc Replace (xóa all rồi insert lại).
/// </summary>
public class ImportReferenceListRequest
{
    public int ReferenceListId { get; set; }
    public ReferenceListImportMode ImportMode { get; set; } = ReferenceListImportMode.Supplement;
}

/// <summary>DTO tóm tắt 1 Reference List (dùng cho danh sách).</summary>
public class ReferenceListSummaryDto
{
    public int Id { get; set; }
    public string ListCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public int ItemCount { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}

/// <summary>DTO 1 dòng item trong Reference List.</summary>
public class ReferenceListItemDto
{
    public int Id { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string? SerialNumber { get; set; }
    public string? ResolvedItemName { get; set; }
    public bool IsActive { get; set; }
    public DateTime ImportedAt { get; set; }
    public string ImportedBy { get; set; } = string.Empty;
    public DateTime? LastUpdatedAt { get; set; }
    public string? Note { get; set; }
    /// <summary>true nếu ItemInstanceId được resolve thành công từ ERP.</summary>
    public bool IsResolvedInERP { get; set; }
}

/// <summary>Kết quả sau khi import Excel.</summary>
public class ImportReferenceListResultDto
{
    public int ReferenceListId { get; set; }
    public string ImportMode { get; set; } = string.Empty;
    public int Inserted { get; set; }
    public int Updated { get; set; }
    public int Deleted { get; set; }       // Chỉ có khi Replace mode
    public int UnresolvedInERP { get; set; }  // Rows không tìm thấy ItemInstance trong ERP
    public int TotalProcessed { get; set; }
}

// ─── Reconciliation Session ───────────────────────────────────────────────────

/// <summary>Request tạo phiên đối soát mới.</summary>
public class CreateReconciliationSessionRequest
{
    public int ReferenceListId { get; set; }
    public string? Note { get; set; }
}

/// <summary>DTO tóm tắt 1 phiên đối soát.</summary>
public class ReconciliationSessionSummaryDto
{
    public int Id { get; set; }
    public string SessionNo { get; set; } = string.Empty;
    public int ReferenceListId { get; set; }
    public string ReferenceListName { get; set; } = string.Empty;
    public int WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public string SessionStatus { get; set; } = string.Empty;
    public DateTime? RunAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int TotalRef { get; set; }
    public int TotalErp { get; set; }
    public int MatchedCount { get; set; }
    public int ERPOnlyCount { get; set; }
    public int RefOnlyCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string? Note { get; set; }
}

// ─── Reconciliation Results ───────────────────────────────────────────────────

/// <summary>Filter khi query kết quả đối soát.</summary>
public class ReconciliationResultFilter
{
    public string? ResultType { get; set; }   // Matched | ERPOnly | RefOnly | (empty = all)
    public string? Keyword { get; set; }      // Search ItemCode hoặc SerialNumber
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

/// <summary>Một dòng kết quả trong phiên đối soát.</summary>
public class ReconciliationResultRowDto
{
    public int Id { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string? SerialNumber { get; set; }
    public string? ResolvedItemName { get; set; }
    /// <summary>Matched | ERPOnly | RefOnly</summary>
    public string ResultType { get; set; } = string.Empty;
    /// <summary>Status của item trong ERP tại thời điểm chạy.</summary>
    public string? ErpStatus { get; set; }
    /// <summary>Vị trí trong ERP tại thời điểm chạy.</summary>
    public string? ErpLocationText { get; set; }
    /// <summary>Chú thích, ví dụ: "Đang mượn".</summary>
    public string? Note { get; set; }
}

/// <summary>Response đầy đủ khi xem kết quả 1 phiên.</summary>
public class ReconciliationSessionResultDto
{
    public ReconciliationSessionSummaryDto Session { get; set; } = new();
    public IReadOnlyCollection<ReconciliationResultRowDto> Results { get; set; } = Array.Empty<ReconciliationResultRowDto>();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
