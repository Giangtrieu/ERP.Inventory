# UI/UX Audit

Ngày kiểm toán: 2026-06-05

## Phần thiết kế tốt nên giữ

- SPA ERP có route rõ theo module: dashboard, inventory, operations, import, reconciliation, quantity inventory.
- Các thao tác post quan trọng đã có modal confirm ở nhiều màn hình.
- Language switch hiện có cơ chế giữ draft thay vì reload mất dữ liệu.
- UI dùng icon Bootstrap và bảng dữ liệu phù hợp ứng dụng vận hành.

## Vấn đề phát hiện

| ID | Mức độ | Mô tả vấn đề | Ảnh hưởng nghiệp vụ | Ảnh hưởng kỹ thuật | Bằng chứng | Phương án xử lý | Độ phức tạp | Rủi ro triển khai |
|---|---|---|---|---|---|---|---|---|
| UX-001 | P1 | Import UI hiển thị và cho thao tác confirm cho mọi role được controller trả Types, trong đó có Viewer. | Người dùng chỉ xem có thể thao tác nhầm hoặc cố ý import nghiệp vụ. | UI không phản ánh quyền ghi; phụ thuộc API chặn nhưng API đang lỏng. | `ImportController.cs:8`, `:21-40`; `import.page.js:7-16`, `:73-77`. | Ẩn upload/confirm theo permission, nhưng quan trọng nhất là sửa API. | Thấp | Cần thông báo quyền rõ cho Viewer. |
| UX-002 | P1 | Import confirm chỉ nói "valid rows will be inserted" nhưng không tóm tắt loại nghiệp vụ, warehouse, số dòng theo nhóm, tác động tồn kho. | Người dùng dễ confirm nhầm file/loại import gây ghi tồn kho hàng loạt. | Thiếu preview tác động trước commit. | `import.page.js:73-77`. | Thêm review step: import type, document count, warehouse, positive/negative stock deltas, risky rows, require checkbox xác nhận. | Trung bình | Cần API preview summary. |
| UX-003 | P2 | Quantity form validate client chưa nhất quán localization: một lỗi dùng literal English. | Người dùng đa ngôn ngữ gặp thông báo lẫn tiếng, thao tác chậm. | Hardcoded message bypass catalog. | `quantity-inventory.page.js:621-624`. | Dùng `UI.msg`/catalog key cho toàn bộ validation client. | Thấp | Không đáng kể. |
| UX-004 | P2 | Quantity transaction table gọi `UI.t` cho dữ liệu động như documentNo/categoryCode. | Mã chứng từ/mã loại hàng có thể bị translate nhầm nếu trùng key; gây hiểu sai dữ liệu. | Trộn translation key với dữ liệu nghiệp vụ. | `quantity-inventory.page.js:420-422`. | Chỉ translate enum/label; dữ liệu động luôn `UI.esc`. | Thấp | Có thể thay đổi hiển thị một số value đang được dịch tình cờ. |
| UX-005 | P2 | Hard delete item instance là thao tác phá hủy nhưng chưa thấy yêu cầu nhập lý do/xác nhận nâng cao. | Admin/Manager có thể xóa nhầm asset và mất lịch sử. | UX không có guard cho destructive data-loss action. | Backend hard delete tại `ManagementController.cs:188-231`; cần đối chiếu UI setup action. | Bắt nhập lý do, hiển thị danh sách document/history liên quan, chỉ cho soft delete mặc định. | Trung bình | Thay đổi thói quen quản trị dữ liệu. |
| UX-006 | P3 | Màn import dùng nhiều card lồng và bảng dài; với file nhiều dòng, review dễ quá tải. | Người dùng khó phát hiện dòng lỗi/nhóm lỗi quan trọng. | UI render toàn rows được trả về, thiếu filter severity/column. | `import.page.js:47-61`. | Thêm filter severity, search row/column, sticky action bar, chỉ tải page lỗi. | Trung bình | Cần API paging rows. |

## Nhận định tổng thể

UI đủ dùng cho vận hành nội bộ, nhưng các màn ghi dữ liệu hàng loạt cần thêm "friction tốt": preview tác động, phân quyền hiển thị, cảnh báo rõ và review dễ quét.
