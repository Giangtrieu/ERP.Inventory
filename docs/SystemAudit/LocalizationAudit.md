# Localization Audit

Ngày kiểm toán: 2026-06-05

## Phần thiết kế tốt nên giữ

- Có `LocalizationCatalog` tập trung cho `vi/en/zh`.
- Frontend có `UI.t`, `UI.msg`, enum localization và cơ chế reload resource khi đổi ngôn ngữ.
- Import template đã dùng localized headers qua `ExcelText`.
- Language switch đã tránh reload toàn trang và có lưu/khôi phục draft.

## Vấn đề phát hiện

| ID | Mức độ | Mô tả vấn đề | Ảnh hưởng nghiệp vụ | Ảnh hưởng kỹ thuật | Bằng chứng | Phương án xử lý | Độ phức tạp | Rủi ro triển khai |
|---|---|---|---|---|---|---|---|---|
| LOC-001 | P1 | Một số file view/template đang có mojibake/encoding lỗi với tiếng Việt/Trung. | Người dùng thấy chữ lỗi trên login hoặc phiếu in nếu resource fallback được dùng. | Encoding repository không đồng nhất, có thể phá bản dịch khi build/deploy. | `Views/Account/Login.cshtml:33-52`, `:61-91`; `core/template.js:1-114`. | Chuẩn hóa UTF-8, bật `.editorconfig charset=utf-8`, kiểm tra encoding trong CI. | Thấp-Trung bình | Cần review thủ công text đã bị hỏng. |
| LOC-002 | P2 | Login page duy trì resource riêng thay vì dùng catalog chung. | Thuật ngữ login có thể lệch với app chính; thêm ngôn ngữ phải sửa nhiều nơi. | Duplication localization. | `Views/Account/Login.cshtml:59-93`. | Dùng endpoint/resource catalog chung hoặc render server-side từ `LocalizationCatalog`. | Trung bình | Cần đảm bảo login vẫn hoạt động trước auth. |
| LOC-003 | P2 | Voucher template hardcode song ngữ Việt-Trung và thiếu nhánh English đầy đủ. | Phiếu in English không nhất quán, khó mở rộng sang ngôn ngữ mới. | Template text nằm trong JS, không version theo catalog/server. | `core/template.js:1-114`. | Chuyển template phrase/sign rows vào catalog hoặc template service theo language. | Trung bình | Cần soát lại format phiếu in. |
| LOC-004 | P2 | Dữ liệu động bị đưa vào `UI.t` ở quantity transaction. | Mã chứng từ/mã hàng có thể bị thay đổi hiển thị nếu trùng key dịch. | Lẫn lộn dữ liệu và translation key. | `quantity-inventory.page.js:420-422`. | Chỉ gọi `UI.t` cho label/enum; dữ liệu động dùng `UI.esc`. | Thấp | Không đáng kể. |
| LOC-005 | P2 | Một số chuỗi client/server vẫn là literal English fallback. | Trải nghiệm đa ngôn ngữ không hoàn chỉnh trong lỗi và confirm quan trọng. | Missing catalog keys không bị phát hiện tự động. | `import.page.js:74-77`; `quantity-inventory.page.js:621-624`; nhiều service return string English như `InventoryCheckService.cs:77-79`, `:369-371`. | Thêm missing-key scanner; chuẩn hóa server message key + localized display. | Trung bình | Cần tránh đổi contract message đột ngột. |
| LOC-006 | P3 | `LocalizationCatalog` quá lớn và monolithic. | Thêm ngôn ngữ/module mới dễ conflict và khó review. | Single file 5,639 dòng, không có cấu trúc module. | `src/ERP.Inventory.Web/Services/LocalizationCatalog.cs` 5,639 dòng. | Tách theo module hoặc resource JSON/RESX, thêm test đủ key `vi/en/zh`. | Trung bình | Cần migration resource format. |

## Nhận định tổng thể

Nền localization tốt hơn nhiều so với hardcode rải rác, nhưng cần xử lý encoding, tách resource theo module và phân biệt dữ liệu động với translation key.
