namespace ERP.Inventory.Domain.Enums;

/// <summary>
/// Kết quả so sánh từng dòng trong phiên đối soát.
/// So sánh chỉ theo (ItemCode, SerialNumber) — không so sánh Status hay Location.
/// </summary>
public enum ReconciliationResultType
{
    /// <summary>Tồn tại trong cả ERP và danh sách tham chiếu.</summary>
    Matched = 1,

    /// <summary>Chỉ có trong ERP, không có trong danh sách tham chiếu.</summary>
    ERPOnly = 2,

    /// <summary>Chỉ có trong danh sách tham chiếu, không tìm thấy trong ERP.</summary>
    RefOnly = 3,
}

/// <summary>
/// Trạng thái của phiên đối soát.
/// </summary>
public enum ReconciliationSessionStatus
{
    /// <summary>Mới tạo, chưa chạy.</summary>
    Draft = 1,

    /// <summary>Đang xử lý so sánh (chạy nền).</summary>
    Running = 2,

    /// <summary>Đã hoàn tất — kết quả có thể xem và xuất.</summary>
    Completed = 3,

    /// <summary>Đã lưu trữ — không hiển thị mặc định.</summary>
    Archived = 4,
}

/// <summary>
/// Chế độ import danh sách tham chiếu.
/// </summary>
public enum ReferenceListImportMode
{
    /// <summary>Bổ sung: upsert theo (ItemCode, SerialNumber), giữ lịch sử.</summary>
    Supplement = 1,

    /// <summary>Thay mới: xóa toàn bộ items cũ rồi import lại.</summary>
    Replace = 2,
}
