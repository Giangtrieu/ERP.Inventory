# Hướng Dẫn Sử Dụng Nghiệp Vụ ERP WMS

## 1. Đăng nhập và quyền truy cập

- Đăng nhập bằng tài khoản đã được cấp trong hệ thống.
- `Admin` xem và thao tác toàn bộ kho, danh mục, tài khoản, báo cáo.
- `Warehouse Manager` thao tác theo các kho được gán quyền.
- `Warehouse Staff` tạo nghiệp vụ theo các kho được gán quyền.
- `Viewer` chỉ xem dữ liệu, không ghi sổ chứng từ.

Khi tài khoản không phải Admin, các dropdown kho, bin, mặt hàng cụ thể, phiếu sửa chữa, phiếu mượn chỉ hiển thị dữ liệu thuộc kho được gán quyền.

## 2. Chuyển đổi ngôn ngữ

- Chọn ngôn ngữ ở góc phải trên cùng: `VI`, `EN`, `ZH`.
- Hệ thống đổi nhãn màn hình, dropdown enum, trạng thái, nhật ký hệ thống, thông báo validate và breadcrumb theo ngôn ngữ đã chọn.
- Nếu vẫn thấy text tiếng Anh sau khi đổi ngôn ngữ, nhấn `Ctrl + F5` để xóa cache trình duyệt.

## 3. Cấu trúc kho

Vào `Cấu trúc kho`.

- Tạo kho mới: để trống `Kho hiện có`, nhập công ty, chi nhánh, kho, khu, kệ, tầng, bin.
- Thêm vị trí mới vào kho cũ: chọn `Kho hiện có`, hệ thống tự kế thừa công ty, chi nhánh và mã kho; người dùng chỉ nhập khu/kệ/tầng/bin mới.
- Một kho có thể có nhiều bin. Danh sách cấu trúc kho hiển thị từng bin thuộc kho đó.
- Bin đã có hàng sẽ không được dùng làm bin đích cho hàng khác khi ghi sổ.

## 4. Nhập kho

Vào `Nhập kho`.

1. Chọn nguồn, kho nhập và ngày nhập.
2. Thêm dòng hàng.
3. Chọn vật tư/SKU, nhập serial hoặc barcode theo dữ liệu thực tế.
4. Chọn bin cho từng dòng.
5. Ghi chú và đính kèm file nếu có.
6. Nhấn `Lưu & ghi sổ`.

Quy tắc chính:

- Cùng một vật tư/SKU có thể nhập nhiều dòng nếu serial/barcode khác nhau.
- Mỗi serial/barcode là duy nhất trong hệ thống.
- Mỗi bin tại cùng thời điểm chỉ chứa một mặt hàng cụ thể.

## 5. Chuyển vị trí

Vào `Chuyển vị trí`.

1. Chọn kho.
2. Chọn mặt hàng cụ thể đang `Trong kho`.
3. Chọn bin đích.
4. Nhấn `Lưu & ghi sổ`.

Hệ thống cập nhật vị trí hiện tại, tồn theo bin và lịch sử phát sinh.

## 6. Gửi sửa chữa

Vào `Gửi sửa chữa`.

1. Chọn đơn vị sửa chữa.
2. Chọn kho.
3. Chọn ngày gửi, ngày dự kiến trả và lý do.
4. Với từng dòng, chọn mặt hàng cụ thể và bin đích của dòng đó.
5. Nhấn `Lưu & ghi sổ`.

Quy tắc chính:

- Chỉ chọn được hàng thuộc kho đã chọn và có trạng thái `Trong kho` hoặc `Hư hỏng`.
- Mỗi dòng phải có bin đích riêng.
- Sau khi ghi sổ, trạng thái hàng là `Đang sửa chữa`; tra cứu hàng sẽ hiển thị vị trí hiện tại theo bin đích, còn đơn vị sửa chữa là bên giữ/đối tác liên quan.

## 7. Nhận sửa chữa

Vào `Nhận sửa chữa`.

1. Chọn phiếu sửa chữa.
2. Chọn kết quả sửa chữa.
3. Chọn kho nhận về.
4. Với từng dòng, chọn mặt hàng cụ thể, bin đích riêng và serial mới nếu có thay thế.
5. Nhấn `Lưu & ghi sổ`.

Quy tắc chính:

- Mỗi mặt hàng nhận sửa chữa cần một bin đích riêng.
- Bin đích không được đang chứa mặt hàng khác.
- Hệ thống ghi lịch sử nhận sửa chữa và cập nhật trạng thái `Trong kho` hoặc `Hư hỏng` theo kết quả.

## 8. Cho mượn

Vào `Cho mượn`.

1. Nhập mã phiếu mượn thủ công.
2. Chọn kho cho mượn.
3. Chọn người mượn, ngày mượn, ngày hạn trả.
4. Nhập bộ phận mượn, người xét duyệt, số điện thoại, chủ quản bộ phận và mục đích.
5. Với từng dòng, chọn mặt hàng cụ thể thuộc kho đã chọn và bin đích riêng.
6. Nhấn `Lưu & ghi sổ`.

Quy tắc chính:

- Tài khoản không phải Admin chỉ chọn được kho được gán quyền.
- Sau khi chọn kho, dropdown mặt hàng chỉ lấy hàng `Trong kho` của kho đó.
- Một mặt hàng cụ thể không được xuất hiện ở hai dòng trong cùng phiếu.
- Một bin đích không được dùng cho hai dòng.

## 9. Nhận trả hàng mượn

Vào `Nhận trả`.

1. Chọn phiếu mượn còn mở.
2. Chọn ngày trả.
3. Chọn kho nhận trả.
4. Với từng dòng, chọn mặt hàng, tình trạng trả và bin đích.
5. Nhấn `Lưu & ghi sổ`.

Nếu tình trạng trả là bình thường hoặc hư hỏng, bắt buộc chọn bin đích. Nếu thất lạc, hệ thống chuyển trạng thái sang thất lạc.

## 10. Báo cáo và dashboard

- `Bảng điều khiển`: xem tổng số hàng, hàng trong kho, sửa chữa, cho mượn, quá hạn trả, hư hỏng/mất.
- Các biểu đồ có filter kho, trạng thái hoặc khoảng ngày.
- Nhấn `Xuất PDF` trên dashboard để in/lưu báo cáo dạng PDF từ trình duyệt.
- `Báo cáo / Nhật ký`: lọc tồn kho, lịch sử phát sinh, audit log theo kho, trạng thái, khoảng ngày, người dùng, hành động và loại dữ liệu.

## 11. File đính kèm

Các phiếu nghiệp vụ hỗ trợ đính kèm file như hóa đơn, biên bản bàn giao, phiếu sửa chữa hoặc ảnh kiểm kê.

File được lưu metadata trong bảng `Attachments` và có thể xem/tải lại từ màn hình chi tiết chứng từ.
