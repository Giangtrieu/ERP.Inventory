# Hướng Dẫn Debug Và Check Lỗi ERP WMS

## 1. Chạy dự án

Từ thư mục gốc dự án:

```powershell
cd "C:\Users\TOANLOC COMPUTER\Documents\Codex\2026-04-26\t-i-ang-c-n-thi\ERP.Inventory"
dotnet run --project .\src\ERP.Inventory.Web\ERP.Inventory.Web.csproj --urls http://localhost:5147
```

Mở trình duyệt:

```text
http://localhost:5147
```

## 2. Build dự án

```powershell
dotnet build .\src\ERP.Inventory.Web\ERP.Inventory.Web.csproj --no-restore -v:minimal
```

Nếu output có dòng `Build succeeded. 0 Error(s)` thì build hợp lệ.

## 3. Lỗi DLL đang bị khóa

Triệu chứng:

```text
The process cannot access the file ... because it is being used by another process.
```

Cách xử lý:

```powershell
Get-Process dotnet
Stop-Process -Id <PID> -Force
dotnet build .\src\ERP.Inventory.Web\ERP.Inventory.Web.csproj --no-restore -v:minimal
```

Nguyên nhân thường là server cũ vẫn đang chạy và giữ DLL trong `bin\Debug\net6.0`.

## 4. Lỗi Visual Studio không chạy được project

Triệu chứng:

```text
A project with an Output Type of Class Library cannot be started directly.
```

Cách xử lý trong Visual Studio:

1. Chuột phải project `ERP.Inventory.Web`.
2. Chọn `Set as Startup Project`.
3. Chạy lại bằng `F5` hoặc `Ctrl + F5`.

Không chạy trực tiếp các project `Domain`, `Application`, `Infrastructure` vì đó là class library.

## 5. Connection string SQL Server

File cấu hình:

```text
src\ERP.Inventory.Web\appsettings.json
```

Connection string hiện dùng dạng:

```json
"DefaultConnection": "Server=LAPTOP-R99AS0H4\\SQLEXPRESS;Database=WarehouseManager;User ID=giang;Password=123456;TrustServerCertificate=True"
```

Nếu lỗi đăng nhập SQL:

- Kiểm tra SQL Server service đang chạy.
- Kiểm tra user `giang` có quyền tạo DB, đọc/ghi bảng.
- Kiểm tra `SQLEXPRESS` đúng tên instance.

## 6. Reset database để test sạch

Cảnh báo: thao tác này xóa toàn bộ dữ liệu trong database `WarehouseManager`.

```powershell
$connection = "Server=LAPTOP-R99AS0H4\SQLEXPRESS;Database=master;User ID=giang;Password=123456;TrustServerCertificate=True"
$sql = "IF DB_ID(N'WarehouseManager') IS NOT NULL BEGIN ALTER DATABASE [WarehouseManager] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [WarehouseManager]; END"
$conn = New-Object System.Data.SqlClient.SqlConnection $connection
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = $sql
$cmd.ExecuteNonQuery()
$conn.Close()
```

Sau đó chạy lại web. EF Core sẽ migrate và seed dữ liệu mẫu.

## 7. Check lỗi API 404

404 nghĩa là frontend gọi endpoint không tồn tại hoặc server đang chạy bản build cũ.

Cách kiểm tra:

1. Dừng toàn bộ tiến trình `dotnet`.
2. Build lại project.
3. Chạy lại server.
4. Mở DevTools của trình duyệt, tab `Network`, xem URL bị 404.

Các endpoint dashboard cần có:

```text
/Dashboard/Summary
/Dashboard/StockByWarehouse
/Dashboard/StockByStatus
/Dashboard/MovementTrend
/Dashboard/MovementByAction
/Dashboard/StockByCategory
/Dashboard/LocationUtilization
/Dashboard/OverdueBorrowAging
```

## 8. Check lỗi API 500

500 là lỗi backend. Cách kiểm tra:

1. Xem terminal đang chạy `dotnet run`.
2. Đọc stack trace đầu tiên có file `.cs` và số dòng.
3. Kiểm tra payload request trong DevTools `Network`.
4. Kiểm tra DB đã migrate đủ chưa.

Các lỗi thường gặp:

- Gửi filter rỗng/null nhưng API chưa xử lý: kiểm tra query string.
- Thiếu migration: chạy lại server để EF migrate.
- Dữ liệu cũ sai cấu trúc: reset database sạch.

## 9. Check lỗi phân quyền kho

Nếu dropdown kho, bin, item không có dữ liệu:

- Đăng nhập Admin để kiểm tra dữ liệu gốc có tồn tại.
- Vào `Hệ thống`, kiểm tra user đã được gán warehouse chưa.
- Với user không phải Admin, API lookup chỉ trả dữ liệu thuộc warehouse được gán quyền.

Nếu backend báo:

```text
Permission denied for ... warehouse.
```

User hiện tại không có quyền thao tác kho đó. Cần Admin gán quyền kho hoặc dùng đúng tài khoản.

## 10. Check lỗi validate nghiệp vụ

Các validate chính:

- Một bin chỉ có một mặt hàng active tại cùng thời điểm.
- Một mặt hàng cụ thể không được xuất hiện ở hai dòng trong cùng phiếu.
- Phiếu gửi sửa, nhận sửa, cho mượn cần bin đích theo từng dòng.
- Tài khoản kho nào chỉ được thao tác dữ liệu kho đó.
- Serial/barcode nhập kho là dữ liệu người dùng nhập, không tự sinh thêm hậu tố.

Frontend sẽ khóa option đã chọn ở dòng khác. Backend vẫn là lớp kiểm tra cuối cùng khi ghi sổ.

## 11. Check đa ngôn ngữ

Nếu thấy text chưa dịch:

1. Tìm key trong `src\ERP.Inventory.Web\Services\LocalizationCatalog.cs`.
2. Kiểm tra JS có gọi `UI.t(...)`, `UI.enum(...)`, `UI.auditAction(...)` hoặc `UI.auditEntity(...)` chưa.
3. Đổi ngôn ngữ trên UI và nhấn `Ctrl + F5`.

Audit log phải hiển thị qua:

```javascript
UI.auditAction(r.action)
UI.auditEntity(r.entityName)
```

Không hiển thị trực tiếp `AuditAction.X` hoặc `AuditEntity.X`.

## 12. Log và file cần xem nhanh

- Backend startup: terminal chạy `dotnet run`.
- Frontend console: DevTools `Console`.
- API request: DevTools `Network`.
- Controller: `src\ERP.Inventory.Web\Controllers`.
- Business service: `src\ERP.Inventory.Infrastructure\Services\InventoryOperationService.cs`.
- Lookup phân quyền: `src\ERP.Inventory.Web\Controllers\LookupController.cs`.
- UI nghiệp vụ: `src\ERP.Inventory.Web\wwwroot\erp\js\pages\operation.page.js`.
- Tài nguyên ngôn ngữ: `src\ERP.Inventory.Web\Services\LocalizationCatalog.cs`.
