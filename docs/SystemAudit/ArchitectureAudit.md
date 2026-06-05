# Architecture Audit

Ngày kiểm toán: 2026-06-05

## Phần thiết kế tốt nên giữ

- Solution tách Domain, Application, Infrastructure và Web tương đối rõ.
- Service layer chứa nghiệp vụ chính, controller chủ yếu orchestration.
- Có DTO, interface service, repository abstraction và `CurrentUserContext`.
- DbContext cấu hình index/relationship tập trung, nhiều FK dùng `DeleteBehavior.Restrict`.
- Lifecycle/rebuild/rollback được đóng gói trong dedicated services.

## Vấn đề phát hiện

| ID | Mức độ | Mô tả vấn đề | Ảnh hưởng nghiệp vụ | Ảnh hưởng kỹ thuật | Bằng chứng | Phương án xử lý | Độ phức tạp | Rủi ro triển khai |
|---|---|---|---|---|---|---|---|---|
| ARCH-001 | P1 | Service quá lớn, nhiều trách nhiệm: import/export 4,466 dòng, lifecycle 3,838 dòng, localization 5,639 dòng. | Thay đổi nghiệp vụ dễ tạo regression, khó review rủi ro tồn kho. | God services, cohesion thấp, test khó. | `ImportExportService.cs` 4,466 dòng; `DocumentLifecycleService.cs` 3,838 dòng; `LocalizationCatalog.cs` 5,639 dòng. | Tách theo bounded context: import parser/validator/poster/exporter; lifecycle per document type; localization per module. | Cao | Refactor lớn cần characterization tests. |
| ARCH-002 | P1 | Transaction/idempotency không được chuẩn hóa qua các service posting. | Import hoặc API retry có thể tạo dữ liệu trùng/không nhất quán. | Mỗi service tự quyết boundary; import wrapper không bao được mọi case. | `ImportExportService.cs:241-260`, `:312-315`; `InventoryCheckService.cs:191-343`, `:373-477`; `QuantityInventoryService.cs:155-270`. | Tạo `PostingUnitOfWork`/idempotency contract chung cho document posting. | Cao | Đòi hỏi test end-to-end từng loại chứng từ. |
| ARCH-003 | P1 | Concurrency là cross-cutting concern nhưng chưa được thể hiện trong domain model. | Race condition làm sai tồn kho khi nhiều người thao tác. | Không có `RowVersion` hoặc lock abstraction trong Domain/Application. | `AuditableEntity.cs:3-10`; `InventoryDbContext.cs:119-151`. | Bổ sung concurrency token và policy retry ở infrastructure. | Cao | Migration và thay đổi lỗi UI. |
| ARCH-004 | P2 | Domain model vẫn anemic: trạng thái và quy tắc chủ yếu nằm trong service. | Quy tắc nghiệp vụ dễ lệch giữa manual/import/edit. | Khó reuse invariant ở nhiều workflow. | `InventoryStatePolicy.cs`; các service tự validate và mutate entity trực tiếp như `MoveLocationService.cs:54-139`. | Đưa invariant quan trọng vào domain/service policy nhỏ, ví dụ `LocationOccupancyPolicy`, `StockMutationPolicy`. | Trung bình-Cao | Cần tránh over-engineering. |
| ARCH-005 | P2 | Controller quản trị chứa logic xóa dữ liệu nghiệp vụ trực tiếp. | Bypass service lifecycle/ledger, rủi ro mất audit. | Web layer thao tác nhiều DbSet và transaction thủ công. | `ManagementController.cs:188-231`. | Chuyển sang application service có policy delete/soft delete, audit reason và dependency preview. | Trung bình | Cần đổi UI/API quản trị. |
| ARCH-006 | P2 | Import/export cùng nằm một service, vừa parse Excel, validate, post nghiệp vụ, generate export. | Thay đổi export có thể ảnh hưởng import posting và ngược lại. | Vi phạm single responsibility. | `ImportExportService.cs` 4,466 dòng; interface gộp import/export tại `IImportExportServices.cs:6-33`. | Tách `IImportService`, `IExportService` implementation riêng; parser/template provider riêng. | Trung bình | DI và test cần cập nhật. |
| API-001 | P2 | Nhiều endpoint trả HTTP 200 kèm `ServiceResult.Success=false` thay vì HTTP status phù hợp. | Client/automation khó phân biệt lỗi nghiệp vụ, lỗi quyền, lỗi hệ thống; dễ retry sai thao tác kho. | API contract không nhất quán, gateway/log/monitoring khó bắt lỗi. | `ImportController.cs:96-113`; `QuantityInventoryController.cs:38-58`; `ImportController.cs:72-78` lại dùng `BadRequest` cho download. | Chuẩn hóa response mapper: validation 400, forbidden 403, not found 404, conflict 409, business dependency 422; giữ body `ServiceResult`. | Trung bình | Frontend hiện có thể đang dựa vào `success=false` với HTTP 200. |
| API-002 | P2 | Validate input chưa có lớp contract validation thống nhất; nhiều service tự trả chuỗi lỗi. | Người dùng nhận lỗi không nhất quán; dữ liệu xấu có thể đi sâu vào service trước khi bị chặn. | Validation phân tán, khó test và khó localization. | `QuantityInventoryService.cs:1674-1679`; `InventoryCheckService.cs:77-79`, `:369-371`; `ImportController.cs:83-96`. | Dùng FluentValidation/DataAnnotations cho DTO boundary, chuẩn hóa error code/message key, validate file upload ở controller/filter. | Trung bình | Cần cập nhật frontend hiển thị lỗi theo field. |

## Nhận định tổng thể

Kiến trúc nền đúng hướng cho ERP nhỏ-trung bình, nhưng các service đã phình to sau nhiều vòng sửa. Giai đoạn tiếp theo nên tập trung vào transaction/idempotency/concurrency trước, rồi mới refactor theo module.
