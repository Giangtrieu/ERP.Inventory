# Executive Summary

Ngày kiểm toán: 2026-06-05

## Điểm sức khỏe hệ thống

**72/100**

Hệ thống có nền nghiệp vụ khá tốt: tách domain/application/infrastructure/web, lifecycle batch cho chứng từ phức tạp, index và transaction history đã được cải thiện. Điểm trừ lớn nằm ở phân quyền import, idempotency khi confirm import, concurrency control và một số thao tác phá hủy dữ liệu ledger.

## Top 10 rủi ro lớn nhất

1. **P0 - Viewer có thể import nghiệp vụ**: đường ghi tồn kho hàng loạt bị mở cho role chỉ xem.
2. **P1 - Import confirm thiếu idempotency toàn cục**: retry sau lỗi batch status có thể ghi trùng nghiệp vụ.
3. **P1 - Thiếu concurrency token/lock hot rows**: thao tác đồng thời có thể gây lệch tồn/vị trí.
4. **P1 - Hard delete item instance xóa ledger/history**: mất audit trail tài sản.
5. **P1 - Import batch/download không scope owner/warehouse**: rò rỉ file import.
6. **P1 - MoveLocation N+1 query**: phiếu nhiều dòng chậm và dễ timeout.
7. **P1 - Export lớn materialize trong memory**: rủi ro RAM/timeout khi dữ liệu tăng.
8. **P2 - Move transaction delta = 0**: ledger transaction không tái dựng được stock movement theo bin.
9. **P2 - Voucher/login localization bị mojibake/hardcode**: phiếu và login đa ngôn ngữ không tin cậy.
10. **P2 - God services quá lớn**: rủi ro regression cao khi sửa nghiệp vụ.

## Top 10 cải tiến quan trọng nhất

1. Khóa quyền import theo `CanOperate/CanManage`, loại Viewer khỏi action ghi.
2. Thêm idempotency key cho import posting.
3. Thêm `RowVersion` cho current location, stock balance, quantity balance.
4. Cấm hard delete asset đã có history/transaction.
5. Scope import batch/rows/download theo user/warehouse.
6. Preload MoveLocation theo batch.
7. Chuyển export lớn sang streaming/background job.
8. Chuẩn hóa transaction move có delta/from-to.
9. Chuẩn hóa UTF-8 và resource catalog.
10. Tách `ImportExportService`, `DocumentLifecycleService`, `LocalizationCatalog` theo module.

## Có thể xử lý nhanh trong 1 ngày

- Sửa role import và service `CanUseImportType`.
- Xóa demo credential khỏi login resource.
- Chặn hard delete item instance khi có bất kỳ ledger/history.
- Sửa `UI.t` trên dữ liệu động trong quantity transaction.
- Catalog hóa các literal validation/confirm dễ thấy.

## Kế hoạch ngắn hạn (1-2 tuần)

- Hoàn tất import authorization + UI permission.
- Thêm import idempotency và trạng thái posting.
- Scope import batches/files theo owner/warehouse.
- Thêm conflict handling cho current location/stock balance.
- Preload MoveLocation để giảm N+1.

## Kế hoạch trung hạn (1-2 tháng)

- Chuẩn hóa transaction boundary cho tất cả document posting.
- Streaming/background export.
- Backfill/chuẩn hóa move transaction.
- Chuyển voucher template vào localization catalog/server.
- Viết E2E test cho import retry, rollback lifecycle và concurrent move/issue.

## Kế hoạch dài hạn (3-6 tháng)

- Refactor god services theo bounded context.
- Modular localization resource và missing-key CI.
- Policy approval cho extra/unknown item trong kiểm kê.
- Observability cho stock mutation: correlation id, document idempotency id, metrics conflict/retry.
- Data reconciliation tool định kỳ: rebuild stock từ current location/transactions và báo lệch.

## Kết luận

ERP.Inventory có xương sống nghiệp vụ tốt và nhiều sửa lỗi quan trọng đã hoàn thành. Tuy vậy, trước khi mở rộng dữ liệu lớn hoặc nhiều người dùng đồng thời, cần ưu tiên xử lý phân quyền import, idempotency, concurrency và bảo toàn ledger.
