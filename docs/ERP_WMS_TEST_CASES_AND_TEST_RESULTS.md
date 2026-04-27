# ERP WMS - Test cases va ket qua kiem thu

Ngay lap: 27/04/2026  
Moi truong: ASP.NET Core MVC .NET 6, EF Core 6, SQL Server `WarehouseManager`, URL test `http://localhost:5147`  
Tai khoan test: `admin/123456`, `manager/123456`, `staff/123456`, `viewer/123456`

## Nguyen tac nghiep vu cot loi

1. Moi `ItemInstance` la mot con hang cu the, dinh danh bang serial/barcode.
2. Mot con hang tai mot thoi diem chi co mot vi tri hien tai trong `CurrentItemLocation`.
3. Mot bin noi bo trong kho chi duoc chua mot con hang active.
4. Nghiep vu nhap kho, nhan sua, nhan tra, dieu chinh ve kho chi duoc chon bin noi bo con trong.
5. Nghiep vu chuyen vi tri duoc phep doi cho hai con hang hoac chuyen day chuyen neu tat ca con hang chiem bin dich cung nam trong cung phieu chuyen.
6. Nghiep vu gui sua chua va cho muon dua hang ra ngoai kho, do do vi tri dich la text nhap tu do, khong phai dropdown bin noi bo.
7. Kiem ke khong tu dong di chuyen hang; kiem ke chi ghi nhan ket qua dem, bin he thong va bin thuc te.
8. Tat ca validate quan trong phai co o backend; frontend chi ho tro ngan nguoi dung thao tac sai som hon.

## Ket qua kiem thu da chay

| Nhom | So case da chay | Pass | Fail | Ghi chu |
| --- | ---: | ---: | ---: | --- |
| Build 4 project | 4 | 4 | 0 | Domain, Application, Infrastructure, Web build thanh cong |
| Migration/startup | 1 | 1 | 0 | Ung dung start duoc voi `ASPNETCORE_ENVIRONMENT=Development` |
| API dashboard | 6 | 6 | 0 | Summary, StockByWarehouse, StockByStatus, MovementTrend, MovementByAction, LocationUtilization |
| API lookup | 3 | 3 | 0 | ItemInstances, Bins availableOnly, MasterDataList filter rong |
| Tong da chay tu dong | 14 | 14 | 0 | Chua thay fail trong smoke test |

## Test cases chi tiet

| ID | Nghiep vu | Du lieu / thao tac | Ket qua mong doi | Trang thai |
| --- | --- | --- | --- | --- |
| AUTH-01 | Dang nhap admin | Dang nhap `admin/123456` | Vao duoc tat ca menu | Can test UI |
| AUTH-02 | Phan quyen kho | Dang nhap staff thuoc 1 kho | Chi thay/chon du lieu thuoc kho duoc gan | Can test UI |
| LANG-01 | Doi ngon ngu | Chuyen VI/EN/ZH | Label, enum, audit, thong bao doi theo ngon ngu | Can test UI |
| DASH-01 | Dashboard PDF | Mo dashboard, bam Export PDF | Tat ca chart SVG hien trong preview PDF | Can test UI |
| DASH-02 | Dashboard filter | Doi kho/ngay/trang thai | Chart reload dung API va doi so lieu | Pass API smoke |
| WH-01 | Them bin vao kho cu | Chon kho co san, nhap zone/rack/shelf/bin | Khong can nhap lai cong ty/chi nhanh/kho | Can test UI |
| WH-02 | Hien thi bin moi | Tao bin moi trong kho | Danh sach cau truc kho hien bin vua tao | Can test UI |
| INB-01 | Nhap kho bin trong | Chon vat tu, serial moi, bin trong | Tao phieu, hang InStock, bin bi chiem | Can test nghiep vu |
| INB-02 | Nhap vao bin da co hang | Chon bin dang co active item | Frontend khong hien bin; backend chan neu co request thu cong | Can test nghiep vu |
| INB-03 | Thieu serial voi hang serial-managed | Bo trong serial | Bao loi ro dong/truong | Can test UI |
| MOVE-01 | Chuyen sang bin trong | Hang A tu bin 1 sang bin 2 trong | A o bin 2, bin 1 trong, stock balance cap nhat | Can test nghiep vu |
| MOVE-02 | Doi cho 2 hang | A: bin 1 -> bin 2, B: bin 2 -> bin 1 trong cung phieu | Thanh cong, khong vi pham 1 bin/1 hang | Can test nghiep vu |
| MOVE-03 | Chuyen day chuyen | A -> bin B, B -> bin C, C -> bin trong | Thanh cong neu moi target chi xuat hien 1 lan | Can test nghiep vu |
| MOVE-04 | Chuyen vao bin bi chiem ngoai phieu | A -> bin cua B, B khong nam trong phieu | Backend tra loi bin bi chiem boi item khong duoc chuyen trong phieu | Can test nghiep vu |
| MOVE-05 | Duplicate target bin | 2 dong cung chon 1 target bin | UI disable/bao loi, backend chan | Can test UI |
| REPAIR-SEND-01 | Gui sua | Chon hang InStock/Damaged, nhap vi tri ngoai kho | Hang Repairing, BinLocationId null, ExternalLocationText co gia tri | Can test nghiep vu |
| REPAIR-SEND-02 | Gui sua thieu vi tri ngoai kho | Bo trong External Destination | UI highlight dong, backend bao loi da ngon ngu | Can test UI |
| REPAIR-RECV-01 | Nhan sua ve kho | Chon phieu sua, hang, bin trong | Hang ve InStock/Damaged theo result, xoa ExternalLocationText | Can test nghiep vu |
| REPAIR-RECV-02 | Nhan 2 hang | Moi dong chon bin dich rieng | UI khong cho trung bin, backend chan trung/bi chiem | Can test UI |
| BORROW-01 | Cho muon | Chon kho, nguoi muon, thong tin bo phan, nhap vi tri ngoai kho | Hang LentOut, vi tri hien tai la vi tri ngoai kho/nguoi muon | Can test nghiep vu |
| BORROW-02 | Cho muon sai kho | Tai khoan staff kho A chon hang kho B | UI khong hien hang/kho B, backend chan | Can test UI |
| BORROW-03 | Thieu thong tin phiếu mượn | Bo trong approver/phone/purpose | UI hien loi ro truong, backend chan | Can test UI |
| RETURN-01 | Nhan tra bin trong | Hang LentOut, condition Normal, bin trong | Hang InStock o bin dich, xoa ExternalLocationText | Can test nghiep vu |
| RETURN-02 | Nhan tra lost | Condition Lost, khong chon bin | Hang Lost, khong chiem bin noi bo | Can test nghiep vu |
| ADJ-01 | Dieu chinh ve bin trong | Chon hang, trang thai moi, bin trong, ly do | Cap nhat status/location, ghi audit/history | Can test nghiep vu |
| ADJ-02 | Dieu chinh vao bin da co hang | Chon target bin occupied | UI khong hien bin occupied, backend chan | Can test UI |
| CHK-01 | Kiem ke khop | Result Matched, actual bin = system bin | Tao phieu kiem ke thanh cong | Can test nghiep vu |
| CHK-02 | Kiem ke sai vi tri | Result WrongLocation, actual bin khac system bin | Tao phieu kiem ke, khong tu dong chuyen hang | Can test nghiep vu |
| CHK-03 | Kiem ke missing | Result Missing, chon item | Tao dong missing | Can test nghiep vu |
| CHK-04 | Kiem ke extra | Result Extra, actual bin bat buoc | Tao dong extra khong can item instance | Can test nghiep vu |
| CHK-05 | Matched nhung actual bin khac | Result Matched, actual != system | Backend bao loi | Can test nghiep vu |
| CHK-06 | WrongLocation nhung actual = system | Result WrongLocation, actual = system | Backend bao loi | Can test nghiep vu |
| IMPORT-01 | Import inbound | File co serial/barcode/bin hop le | Insert du lieu, tao phieu, lich su | Can test file |
| IMPORT-02 | Import repair send | File co `TargetExternalLocation` | Gui sua thanh cong, vi tri ngoai kho dung | Can test file |
| EXPORT-01 | Export history filter ngay | Chon tu ngay/den ngay | File chi co du lieu trong khoang ngay | Can test file |
| ATT-01 | Upload attachment | Upload pdf/png/xlsx nho hon gioi han | Attachment luu DB va tai lai duoc | Can test file |
| MASTER-01 | MasterDataList filter rong | Khong chon entity/filter | API tra ve danh sach vat tu hop le, UI select mac dinh Items | Pass API smoke |
| MASTER-02 | Tao vat tu moi | Nhap item, category, unit, ban dich | Lookup vat tu reload khong mat filter dang thao tac | Can test UI |

## Cach test thao tac nghiep vu mau

1. Dang nhap `admin/123456`.
2. Vao `Cau truc kho`, tao them it nhat 3 bin trong cung mot kho de test move/swap.
3. Vao `Nhap kho`, nhap 2 mat hang cung SKU nhung khac serial vao 2 bin khac nhau.
4. Vao `Chuyen vi tri`, tao phieu doi cho 2 serial vua nhap.
5. Tra cuu tung serial o `Tra cuu hang`, xac nhan vi tri da doi dung.
6. Vao `Gui sua chua`, chon 1 serial, nhap `External Destination` nhu `Vendor A / Repair shelf 01`.
7. Tra cuu serial, xac nhan `Current Location` hien vi tri ngoai kho, khong phai ten nguoi/ten vendor don thuan.
8. Vao `Nhan sua chua`, chon serial va mot bin trong, xac nhan hang quay ve kho.
9. Vao `Cho muon`, chon kho, nguoi muon, thong tin bo phan, nhap `External Destination`.
10. Vao `Nhan tra`, tra hang ve mot bin trong, xac nhan hang ve `InStock`.
11. Vao `Kiem ke`, test Matched/WrongLocation/Missing/Extra nhu bang case tren.
12. Vao `Dashboard`, doi filter va in PDF, xac nhan moi bieu do deu hien trong print preview.

## Ghi chu debug nhanh

- Neu build bao DLL bi khoa: dung tien trinh `dotnet` dang chay roi build lai.
- Neu login test bang `dotnet exec`, phai chay trong thu muc `src/ERP.Inventory.Web` de doc dung `appsettings.json`.
- Neu API SQL bao LocalDB, kiem tra `ContentRootPath` va `ASPNETCORE_ENVIRONMENT`.
- Neu dropdown bin khong co gia tri, kiem tra API `/Lookup/Bins?warehouseId=<id>&availableOnly=true` va bang `CurrentItemLocations`.
- Neu tracking sai vi tri ngoai kho, kiem tra `CurrentItemLocations.ExternalLocationText` va `ItemMovementHistories.ToLocationDisplay`.
