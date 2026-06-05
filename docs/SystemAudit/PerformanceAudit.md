# Performance Audit

Ngày kiểm toán: 2026-06-05

## Phần thiết kế tốt nên giữ

- Đã bổ sung nhiều index quan trọng cho current location, movement history, transactions, quantity và lifecycle batch.
- Import validation đã có preload context, giảm N+1 so với bản cũ.
- Report preview đã được giới hạn số dòng theo tài liệu triển khai trước.
- Các truy vấn đọc chính dùng `AsNoTracking()` ở nhiều nơi.

## Vấn đề phát hiện

| ID | Mức độ | Mô tả vấn đề | Ảnh hưởng nghiệp vụ | Ảnh hưởng kỹ thuật | Bằng chứng | Phương án xử lý | Độ phức tạp | Rủi ro triển khai |
|---|---|---|---|---|---|---|---|---|
| PERF-001 | P1 | Move location vẫn có N+1 query theo từng line: tìm instance, bin, current location và occupant. | Phiếu chuyển nhiều dòng chậm, dễ timeout trong giờ cao điểm. | Số query tăng tuyến tính theo số dòng. | `MoveLocationService.cs:54-80`; occupant query trong loop `:76-93`. | Preload instances, bins, current locations và target-bin occupants theo tập code/bin trong request. | Trung bình | Phải giữ nguyên validation message/ordering. |
| PERF-002 | P1 | Export/report document materialize nhiều document với `Include(Lines...)` rồi `SelectMany` trong memory, giới hạn cứng 5k/50k. | File lớn làm chậm app, tốn RAM, có thể timeout khi dữ liệu tăng. | Không streaming; projection sau `ToListAsync`. | `ImportExportService.cs:713-822`, `:843-860`. | Projection trực tiếp sang DTO/object rows trong SQL, paging/streaming export, background job cho export lớn. | Trung bình-Cao | Thay đổi export có thể ảnh hưởng định dạng file. |
| PERF-003 | P1 | Không có concurrency token khiến retry/ghi đè hot rows dùng last-write-wins; khi nhiều user thao tác, hiệu năng và đúng đắn cùng suy giảm do deadlock/race. | Dữ liệu tồn có thể lệch khi thao tác đồng thời, hoặc người dùng gặp lỗi DB khó hiểu. | EF không phát hiện stale writes; phải dựa DB unique exception. | `AuditableEntity.cs:3-10`; `InventoryOperationBase.cs:224-240`; `InventoryDbContext.cs:119-128`, `:140-151`. | Thêm `RowVersion`, retry policy có backoff, lock theo item/bin/balance hot path. | Cao | Cần migration và xử lý lỗi UI. |
| PERF-004 | P2 | Import list chỉ lấy 50 batch toàn hệ thống, chưa filter theo user/warehouse/status. | Người dùng khó tìm batch của mình khi dữ liệu lớn; Admin phải tải danh sách không liên quan. | Query không tận dụng scope nghiệp vụ; không có paging API. | `ImportExportService.cs:317-334`. | Thêm paging/filter/status/createdBy/warehouse scope cho import batch list. | Thấp-Trung bình | Cần cập nhật UI import. |
| PERF-005 | P2 | `QuantityInventoryService` còn nhiều method/đường cũ có `SaveChangesAsync` và lookup trong loop, làm tăng rủi ro tái sử dụng nhầm. | Tính năng mới có thể gọi nhầm đường chậm, gây timeout hoặc dữ liệu trung gian commit nhiều lần. | Technical debt hiệu năng trong service 1.9k dòng. | `QuantityInventoryService.cs:443-547`, `:1273-1431`, tổng file 1,923 dòng. | Xóa/đánh dấu obsolete các đường không dùng; tách posting engine đã preload thành service riêng. | Trung bình | Cần test regression quantity. |
| PERF-006 | P2 | `LocalizationCatalog` 5,639 dòng và load resource monolithic có thể tăng payload/parse trên client khi thêm ngôn ngữ. | Chuyển ngôn ngữ hoặc boot app nặng dần theo số module/ngôn ngữ. | Không chia resource theo module/route. | `LocalizationCatalog.cs` 5,639 dòng; frontend load resources toàn cục trong `app.js` theo thiết kế hiện tại. | Tách catalog theo module, lazy-load route resources, kiểm tra missing key trong CI. | Trung bình | Cần đổi contract localization. |

## Nhận định tổng thể

Hệ thống đã có nhiều cải tiến hiệu năng nền. Điểm cần làm tiếp là xử lý N+1 ở posting nhiều dòng, export streaming và concurrency token để vừa tăng hiệu năng vừa giảm sai lệch dữ liệu khi nhiều người dùng.
