# Implementation Roadmap

Ngày lập: 2026-06-05

## Nguyên tắc ưu tiên

1. Chặn đường gây sai lệch tồn kho hoặc ghi nghiệp vụ trái quyền.
2. Bảo vệ dữ liệu ledger/audit trước khi tối ưu UI.
3. Thêm idempotency và concurrency cho hot paths.
4. Sau khi nghiệp vụ ổn định mới refactor service lớn.

## Quick wins trong 1 ngày

| Hạng mục | Mức độ | Việc cần làm | Kết quả mong đợi | Rủi ro |
|---|---|---|---|---|
| SEC/BL-001 | P0 | Loại `Viewer` khỏi upload/confirm import; service check `user.CanOperate` cho import nghiệp vụ. | Chặn ghi tồn kho trái quyền. | Có thể cần chỉnh UI menu. |
| SEC-006 | P2 | Xóa demo credential khỏi login view/resource. | Giảm lộ thông tin nhạy cảm. | Thấp. |
| UX/LOC-004 | P2 | Thay `UI.t(r.documentNo/itemCategoryCode)` bằng `UI.esc`. | Không translate nhầm dữ liệu động. | Thấp. |
| UX/LOC-005 | P2 | Catalog hóa các literal còn thấy trong import/quantity validation. | Thông báo nhất quán đa ngôn ngữ. | Thấp. |
| BL-003 | P1 | Chặn hard delete item instance nếu có bất kỳ history/transaction/document line. | Bảo toàn ledger. | Cần thông báo thay thế soft delete. |

## Ngắn hạn (1-2 tuần)

| Hạng mục | Mức độ | Việc cần làm | Kết quả mong đợi | Rủi ro |
|---|---|---|---|---|
| BL-002 | P1 | Thiết kế idempotency cho import confirm, thêm trạng thái `Posting`/`PostedMarker`. | Retry import không tạo nghiệp vụ trùng. | Cần migration/test. |
| SEC-002 | P1 | Scope import batch/rows/download theo createdBy/warehouse/role. | Không lộ file import giữa users. | Cần bổ sung metadata batch. |
| PERF-001 | P1 | Preload MoveLocation theo batch. | Phiếu nhiều dòng nhanh hơn, ít query hơn. | Giữ nguyên validation. |
| BL-004 | P1 | Thêm optimistic concurrency cho current location và stock balance hot rows. | Giảm sai lệch khi thao tác đồng thời. | UI phải xử lý conflict. |
| UX-002 | P1 | Import confirm preview tác động tồn kho. | Người dùng confirm an toàn hơn. | Cần API summary. |

## Trung hạn (1-2 tháng)

| Hạng mục | Mức độ | Việc cần làm | Kết quả mong đợi | Rủi ro |
|---|---|---|---|---|
| ARCH-002 | P1 | Chuẩn hóa posting transaction/idempotency contract cho tất cả document services. | Boundary nhất quán. | Refactor rộng. |
| PERF-002 | P1 | Streaming/background export, projection SQL trực tiếp. | Export dữ liệu lớn ổn định. | Đổi định dạng có thể ảnh hưởng user. |
| LOC-001/003 | P1-P2 | Chuẩn hóa UTF-8 và chuyển voucher template vào catalog/server. | Phiếu in đa ngôn ngữ đúng. | Cần QA layout phiếu. |
| BL-005 | P2 | Chuẩn hóa transaction cho move delta hoặc schema from/to. | Ledger tái dựng được tồn theo bin. | Backfill cần cẩn trọng. |
| ARCH-005 | P2 | Đưa hard-delete/soft-delete vào application service. | Web layer không bypass nghiệp vụ. | Cần đổi API/UI. |

## Dài hạn (3-6 tháng)

| Hạng mục | Mức độ | Việc cần làm | Kết quả mong đợi | Rủi ro |
|---|---|---|---|---|
| ARCH-001/006 | P2 | Tách god services theo bounded context. | Bảo trì và test tốt hơn. | Cần characterization tests. |
| PERF-003 | P1 | Locking/retry strategy cho toàn bộ hot path tồn kho. | Mở rộng nhiều user và dữ liệu lớn. | Có thể tăng contention. |
| LOC-006 | P3 | Resource modular hóa + CI missing-key scanner. | Thêm ngôn ngữ dễ hơn. | Đổi pipeline build. |
| BL-006 | P2 | Policy approval cho extra/unknown item trong kiểm kê. | Kiểm soát tạo tài sản từ kiểm kê. | Thay đổi workflow kho. |
| Test suite | P1 | E2E scenario: concurrent move/issue, import retry, rollback lifecycle. | Giảm regression nghiệp vụ. | Đầu tư test data/harness. |

## Thứ tự triển khai đề xuất

1. P0 import authorization.
2. Hard delete ledger guard.
3. Import idempotency và scope dữ liệu import.
4. Concurrency token cho current location/balance.
5. N+1 move posting và export lớn.
6. Localization encoding/template.
7. Refactor service lớn sau khi có test.
