# Security Audit

Ngày kiểm toán: 2026-06-05

## Phần thiết kế tốt nên giữ

- Hầu hết controller có `[Authorize]`; các endpoint ghi dữ liệu quan trọng có `[ValidateAntiForgeryToken]`.
- `CurrentUserContext` có abstraction quyền theo role và warehouse scope.
- SuperPassword đã được tách bằng `AuthMode = Super` và không còn phụ thuộc user thật theo tài liệu triển khai hiện có.
- Có audit/error logging tập trung cho exception và thao tác import.

## Vấn đề phát hiện

| ID | Mức độ | Mô tả vấn đề | Ảnh hưởng nghiệp vụ | Ảnh hưởng kỹ thuật | Bằng chứng | Phương án xử lý | Độ phức tạp | Rủi ro triển khai |
|---|---|---|---|---|---|---|---|---|
| SEC-001 | P0 | Viewer được phép dùng ImportController và import service chỉ chặn master-data. | Viewer có thể ghi nghiệp vụ kho hàng loạt, gây nhập/xuất/kiểm kê/mượn/sửa chữa trái phép. | Broken authorization ở boundary nguy hiểm nhất. | `ImportController.cs:8`, `:81-113`, `:116-120`; `ImportExportService.cs:99-102`, `:220-244`, `:3776-3779`. | Tách quyền `Import.Read`, `Import.Operate`, `Import.MasterData`; loại Viewer khỏi confirm/upload nghiệp vụ. | Thấp | Cần cập nhật UI menu để Viewer không thấy action ghi. |
| SEC-002 | P1 | Import batches/rows/download không scope theo người tạo hoặc warehouse; người có quyền loại import có thể xem file/batch của người khác. | Rò rỉ file import có serial, số lượng, vị trí, nhà cung cấp/người mượn. | Insecure direct object reference theo `importBatchId`. | `ImportExportService.cs:317-334`, `:337-353`, `:370-416`; controller truyền id trực tiếp `ImportController.cs:62-78`. | Lưu và kiểm tra owner/warehouse scope của batch; chỉ Admin/Manager xem tất cả, Staff xem batch của mình/warehouse được phép. | Trung bình | Cần migration nếu thiếu warehouse/created-by normalized fields. |
| SEC-003 | P1 | Endpoint hard delete item instance cho `Admin,Warehouse Manager` xóa dữ liệu ledger rộng. | Manager có thể làm mất lịch sử tài sản và che dấu sai lệch tồn kho. | Quyền destructive quá rộng, không có dual control/approval. | `ManagementController.cs:188-231`. | Chỉ Admin hoặc Super mới hard-delete dữ liệu chưa phát sinh; với dữ liệu đã phát sinh chỉ soft delete và audit reason. | Thấp | Có thể chặn thao tác dọn dữ liệu test; cần công cụ cleanup riêng cho dev. |
| SEC-004 | P2 | Upload import chỉ kiểm tra file rỗng, chưa thấy giới hạn kích thước/type content thực tế. | Người dùng có thể tải file quá lớn gây đầy bộ nhớ/DB hoặc timeout. | DoS qua upload và lưu `OriginalFileContent`. | `ImportController.cs:83-96`; `ImportExportService.cs:91-158`. | Giới hạn kích thước theo cấu hình, validate extension + MIME + magic bytes, stream parse, quota theo user. | Thấp-Trung bình | File import lớn hợp lệ cần quy trình batch riêng. |
| SEC-005 | P2 | Export nhiều endpoint chỉ `[Authorize]`; phụ thuộc service scope nên controller không thể hiện quyền nghiệp vụ rõ ràng. | Người dùng có thể xuất nhiều dữ liệu hơn kỳ vọng nếu service scope lỗi. | Defense-in-depth yếu ở API boundary. | `ExportController.cs:21-32`, `:45-97`; chỉ một số endpoint master/audit có role ở `:35-40`, `:101-113`. | Thêm policy/role theo từng nhóm export; giữ warehouse scope trong service. | Thấp | Có thể cần phân quyền Viewer rõ hơn cho báo cáo. |
| SEC-006 | P2 | Login view còn chứa thông tin demo account/password trong comment và resource object. | Lộ thói quen mật khẩu mặc định trong source/package; nếu seed production không đổi mật khẩu sẽ rủi ro. | Secret-like content tồn tại trong frontend artifact. | `Views/Account/Login.cshtml:54-56`, `:69-80`, `:91`. | Xóa hoàn toàn demo credential khỏi view; chỉ hiển thị trong docs dev nội bộ. | Thấp | Không đáng kể. |

## Nhận định tổng thể

Rủi ro bảo mật lớn nhất không phải thiếu login mà là phân quyền nghiệp vụ chưa đủ chặt ở import. Đây là đường có thể thay đổi tồn kho hàng loạt nên cần xử lý trước các vấn đề UI hoặc hardening phụ.
