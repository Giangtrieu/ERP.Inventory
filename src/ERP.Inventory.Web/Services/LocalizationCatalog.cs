using ERP.Inventory.Domain.Enums;



namespace ERP.Inventory.Web.Services;



public static class LocalizationCatalog

{

    public static IReadOnlyDictionary<string, string> Resources(string language)

    {

        return Normalize(language) switch

        {

            "en" => Merge(En, EnSupplement),

            "zh" => Merge(Zh, ZhSupplement),

            _ => Merge(Vi, ViSupplement)

        };

    }



    public static string Text(string language, string key)

    {

        var resources = Resources(language);

        return resources.TryGetValue(key, out var value) ? value : key;

    }



    public static string EnumText<TEnum>(string language, TEnum value) where TEnum : struct, Enum

    {

        return Text(language, $"Enum.{typeof(TEnum).Name}.{value}");

    }



    public static object[] EnumOptions<TEnum>(string language) where TEnum : struct, Enum

    {

        return Enum.GetNames<TEnum>()

            .Select(x => new { id = x, text = Text(language, $"Enum.{typeof(TEnum).Name}.{x}") })

            .ToArray<object>();

    }



    public static object[] AllEnumOptions(string language)

    {

        return new object[]

        {

            new { name = nameof(ItemStatus), values = EnumOptions<ItemStatus>(language) },

            new { name = nameof(InventoryCheckLineResult), values = EnumOptions<InventoryCheckLineResult>(language) },

            new { name = nameof(RepairResult), values = EnumOptions<RepairResult>(language) },

            new { name = nameof(BorrowReturnCondition), values = EnumOptions<BorrowReturnCondition>(language) },

            new { name = nameof(ExternalPartyType), values = EnumOptions<ExternalPartyType>(language) },

            new { name = nameof(MovementActionType), values = EnumOptions<MovementActionType>(language) },

            new { name = nameof(ImportBatchStatus), values = EnumOptions<ImportBatchStatus>(language) },

            new { name = nameof(ValidationSeverity), values = EnumOptions<ValidationSeverity>(language) },

            new { name = nameof(InventoryTransactionType), values = EnumOptions<InventoryTransactionType>(language) },

            new { name = nameof(QuantityInventoryDocumentType), values = EnumOptions<QuantityInventoryDocumentType>(language) },

            new { name = nameof(ItemTrackingType), values = EnumOptions<ItemTrackingType>(language) },

            new { name = nameof(ItemStatusView), values = EnumOptions<ItemStatusView>(language) },

            new { name = nameof(InventoryStatus), values = EnumOptions<InventoryStatus>(language) },

            new { name = nameof(DocumentPeriodType), values = EnumOptions<DocumentPeriodType>(language) },

            new { name = nameof(ReconciliationResultType), values = EnumOptions<ReconciliationResultType>(language) },

            new { name = nameof(ReconciliationSessionStatus), values = EnumOptions<ReconciliationSessionStatus>(language) },

            new { name = nameof(ReferenceListImportMode), values = EnumOptions<ReferenceListImportMode>(language) },

            new { name = nameof(LocationType), values = EnumOptions<LocationType>(language) },

            new { name = nameof(DocumentStatus), values = EnumOptions<DocumentStatus>(language) }

        };

    }



    private static string Normalize(string? language)

    {

        return language?.ToLowerInvariant() switch

        {

            "en" => "en",

            "zh" => "zh",

            _ => "vi"

        };

    }



    private static IReadOnlyDictionary<string, string> Merge(IReadOnlyDictionary<string, string> baseResources, IReadOnlyDictionary<string, string> supplement)

    {

        var merged = new Dictionary<string, string>(baseResources);

        foreach (var item in supplement)

        {

            merged[item.Key] = item.Value;

        }

        return merged;

    }



    private static readonly Dictionary<string, string> ViSupplement = new()

    {
        ["SystemError.UserMessage"] = "Có lỗi hệ thống. Mã lỗi: {0}. Vui lòng liên hệ TE/IT.",
        ["Error Management"] = "Quản lý lỗi hệ thống",
        ["SuperAdmin Password"] = "Mật khẩu SuperAdmin",
        ["Unlock Error Management"] = "Mở khóa quản lý lỗi",
        ["System Errors"] = "Lỗi hệ thống",
        ["Error Code"] = "Mã lỗi",
        ["Module"] = "Phân hệ",
        ["Request Path"] = "Đường dẫn",
        ["HTTP Method"] = "Phương thức HTTP",
        ["Error message"] = "Thông báo lỗi",
        ["Detail"] = "Chi tiết",
        ["Resolved by"] = "Người xử lý",
        ["Client IP"] = "IP máy khách",
        ["Browser"] = "Trình duyệt",
        ["Resolved"] = "Đã xử lý",
        ["Unresolved"] = "Chưa xử lý",
        ["Mark resolved"] = "Đánh dấu đã xử lý",
        ["Mark this system error as resolved?"] = "Đánh dấu lỗi hệ thống này là đã xử lý?",
        ["Resolution notes"] = "Ghi chú xử lý",
        ["Stack Trace"] = "Stack trace",
        ["Payload JSON"] = "Payload JSON",
        ["Inner Exception"] = "Inner exception",
        ["SuperAdmin password is invalid or not configured."] = "Mật khẩu SuperAdmin không đúng hoặc chưa được cấu hình.",
        ["Error log not found."] = "Không tìm thấy log lỗi.",

        ["Quantity Inventory"] = "Tồn kho số lượng",

        ["Quantity Stock"] = "Tồn số lượng",

        ["Quantity Transactions"] = "Lịch sử tồn số lượng",

        ["Quantity Operation"] = "Nghiệp vụ số lượng",

        ["Receive"] = "Nhập",

        ["Issue"] = "Xuất",

        ["Adjust"] = "Điều chỉnh",

        ["SN"] = "SN",

        ["Qty"] = "SL",

        ["Quantity"] = "Số lượng",

        ["Quantity inventory posted."] = "Đã ghi sổ tồn kho số lượng.",

        ["Quantity must be greater than zero."] = "Số lượng phải lớn hơn 0.",

        ["SN is required."] = "Bắt buộc nhập SN.",

        ["Access denied for selected warehouse."] = "Không có quyền thao tác kho đã chọn.",

        ["Permission denied for this warehouse."] = "Không có quyền truy cập kho này.",

        ["Permission denied for inventory check warehouse."] = "Không có quyền thao tác kho kiểm kê.",

        ["Warehouse is invalid."] = "Kho không hợp lệ.",

        ["Current role cannot manage this warehouse."] = "Vai trò hiện tại không thể quản lý kho này.",

        ["Current role cannot use this import type."] = "Vai trò hiện tại không thể dùng loại import này.",

        ["Bin code already exists in warehouse."] = "Mã bin đã tồn tại trong kho.",

        ["User name already exists."] = "Tên đăng nhập đã tồn tại.",

        ["You cannot remove your own admin access."] = "Không thể tự gỡ quyền admin của chính bạn.",

        ["You cannot deactivate your own account."] = "Không thể tự khóa tài khoản của chính bạn.",

        ["You cannot hard delete your own account."] = "Không thể tự xóa hẳn tài khoản của chính bạn.",

        ["No file uploaded."] = "Chưa tải file lên.",

        ["File is empty."] = "File trống.",

        ["Category code already exists."] = "Mã nhóm hàng đã tồn tại.",

        ["Party type is invalid."] = "Loại đối tác không hợp lệ.",

        ["Party code already exists."] = "Mã đối tác đã tồn tại.",

        ["Item code already exists."] = "Mã vật tư đã tồn tại.",

        ["Category is invalid."] = "Nhóm hàng không hợp lệ.",

        ["Cannot hard delete this record because it is referenced by operational data. Use soft delete instead."] = "Không thể xóa hẳn vì dữ liệu đã được nghiệp vụ tham chiếu. Hãy dùng ngưng dùng.",

        ["Target external location is required for borrowed item."] = "Bắt buộc nhập vị trí ngoài cho hàng mượn.",

        ["Borrowed item must belong to the selected warehouse."] = "Hàng mượn phải thuộc kho đã chọn.",

        ["DocumentNo {0} already exists."] = "Mã chứng từ {0} đã tồn tại.",

        ["Insufficient quantity for item {0} SN {1}."] = "Không đủ số lượng cho vật tư {0}, SN {1}.",

        ["Enum.QuantityInventoryDocumentType.Receive"] = "Nhập",

        ["Enum.QuantityInventoryDocumentType.Issue"] = "Xuất",

        ["Enum.QuantityInventoryDocumentType.Adjust"] = "Điều chỉnh",

        ["Audit Trail"] = "Lịch sử kiểm tra",

        ["AuditEntity.QuantityInventoryDocument"] = "Phiếu tồn số lượng",

        ["AuditAction.Receive"] = "Nhập tồn số lượng",

        ["AuditAction.Issue"] = "Xuất tồn số lượng",

        ["AuditAction.Adjust"] = "Điều chỉnh tồn số lượng",

        ["Enum.ItemTrackingType.LocationTracked"] = "Theo dõi vị trí",

        ["Enum.ItemTrackingType.QuantityOnly"] = "Quản lý số lượng",

        ["Enum.ExternalPartyType.Receiver"] = "Người nhập",

        ["Enum.ReconciliationResultType.Matched"] = "Khớp",

        ["Enum.ReconciliationResultType.ERPOnly"] = "Chỉ có trong ERP",

        ["Enum.ReconciliationResultType.RefOnly"] = "Chỉ có trong danh sách tham chiếu",

        ["Enum.ReconciliationSessionStatus.Draft"] = "Nháp",

        ["Enum.ReconciliationSessionStatus.Running"] = "Đang chạy",

        ["Enum.ReconciliationSessionStatus.Completed"] = "Hoàn tất",

        ["Enum.ReconciliationSessionStatus.Archived"] = "Đã lưu trữ",

        ["Enum.ReferenceListImportMode.Supplement"] = "Bổ sung",

        ["Enum.ReferenceListImportMode.Replace"] = "Thay thế",

        ["Export Quantity Balance"] = "Xuat ton so luong",

        ["Export Inbound Documents"] = "Xuat phieu nhap kho",

        ["Export Quantity Transactions"] = "Xuat giao dich so luong",

        ["Export Adjustment Documents"] = "Xuat phieu dieu chinh",

        ["Export Inventory Check Documents"] = "Xuat phieu kiem ke",

        ["Export Move Documents"] = "Xuat phieu chuyen vi tri",

        ["Export Repair Documents"] = "Xuat phieu sua chua",

        ["Export Borrow Documents"] = "Xuat phieu cho muon",

        ["Export Warehouse Structure"] = "Xuat cau truc kho",

        ["Export Item Catalog"] = "Xuat danh muc vat tu"

    };



    private static readonly Dictionary<string, string> EnSupplement = new()

    {
        ["SystemError.UserMessage"] = "System error occurred. Error code: {0}. Please contact TE/IT.",
        ["Error Management"] = "Error Management",
        ["SuperAdmin Password"] = "SuperAdmin Password",
        ["Unlock Error Management"] = "Unlock Error Management",
        ["System Errors"] = "System Errors",
        ["Error Code"] = "Error Code",
        ["Module"] = "Module",
        ["Request Path"] = "Request Path",
        ["HTTP Method"] = "HTTP Method",
        ["Error message"] = "Error message",
        ["Detail"] = "Detail",
        ["Resolved by"] = "Resolved by",
        ["Client IP"] = "Client IP",
        ["Browser"] = "Browser",
        ["Resolved"] = "Resolved",
        ["Unresolved"] = "Unresolved",
        ["Mark resolved"] = "Mark resolved",
        ["Mark this system error as resolved?"] = "Mark this system error as resolved?",
        ["Resolution notes"] = "Resolution notes",
        ["Stack Trace"] = "Stack Trace",
        ["Payload JSON"] = "Payload JSON",
        ["Inner Exception"] = "Inner Exception",
        ["SuperAdmin password is invalid or not configured."] = "SuperAdmin password is invalid or not configured.",
        ["Error log not found."] = "Error log not found.",

        ["Quantity Inventory"] = "Quantity Inventory",

        ["Quantity Stock"] = "Quantity Stock",

        ["Quantity Transactions"] = "Quantity Transactions",

        ["Quantity Operation"] = "Quantity Operation",

        ["Receive"] = "Receive",

        ["Issue"] = "Issue",

        ["Adjust"] = "Adjust",

        ["SN"] = "SN",

        ["Qty"] = "Qty",

        ["Quantity"] = "Quantity",

        ["Quantity inventory posted."] = "Quantity inventory posted.",

        ["Quantity must be greater than zero."] = "Quantity must be greater than zero.",

        ["SN is required."] = "SN is required.",

        ["Access denied for selected warehouse."] = "Access denied for selected warehouse.",

        ["Permission denied for this warehouse."] = "Permission denied for this warehouse.",

        ["Permission denied for inventory check warehouse."] = "Permission denied for inventory check warehouse.",

        ["Warehouse is invalid."] = "Warehouse is invalid.",

        ["Current role cannot manage this warehouse."] = "Current role cannot manage this warehouse.",

        ["Current role cannot use this import type."] = "Current role cannot use this import type.",

        ["Bin code already exists in warehouse."] = "Bin code already exists in warehouse.",

        ["User name already exists."] = "User name already exists.",

        ["You cannot remove your own admin access."] = "You cannot remove your own admin access.",

        ["You cannot deactivate your own account."] = "You cannot deactivate your own account.",

        ["You cannot hard delete your own account."] = "You cannot hard delete your own account.",

        ["No file uploaded."] = "No file uploaded.",

        ["File is empty."] = "File is empty.",

        ["Category code already exists."] = "Category code already exists.",

        ["Party type is invalid."] = "Party type is invalid.",

        ["Party code already exists."] = "Party code already exists.",

        ["Item code already exists."] = "Item code already exists.",

        ["Category is invalid."] = "Category is invalid.",

        ["Cannot hard delete this record because it is referenced by operational data. Use soft delete instead."] = "Cannot hard delete this record because it is referenced by operational data. Use soft delete instead.",

        ["Target external location is required for borrowed item."] = "Target external location is required for borrowed item.",

        ["Borrowed item must belong to the selected warehouse."] = "Borrowed item must belong to the selected warehouse.",

        ["DocumentNo {0} already exists."] = "DocumentNo {0} already exists.",

        ["Insufficient quantity for item {0} SN {1}."] = "Insufficient quantity for item {0} SN {1}.",

        ["Enum.QuantityInventoryDocumentType.Receive"] = "Receive",

        ["Enum.QuantityInventoryDocumentType.Issue"] = "Issue",

        ["Enum.QuantityInventoryDocumentType.Adjust"] = "Adjust",

        ["Audit Trail"] = "审计追踪",

        ["AuditEntity.QuantityInventoryDocument"] = "Quantity inventory document",

        ["AuditAction.Receive"] = "Quantity receive",

        ["AuditAction.Issue"] = "Quantity issue",

        ["AuditAction.Adjust"] = "Quantity adjustment",

        ["Enum.ItemTrackingType.LocationTracked"] = "Location tracked",

        ["Enum.ItemTrackingType.QuantityOnly"] = "Quantity only",

        ["Enum.ExternalPartyType.Receiver"] = "Receiver",

        ["Enum.ReconciliationResultType.Matched"] = "Matched",

        ["Enum.ReconciliationResultType.ERPOnly"] = "ERP only",

        ["Enum.ReconciliationResultType.RefOnly"] = "Reference only",

        ["Enum.ReconciliationSessionStatus.Draft"] = "Draft",

        ["Enum.ReconciliationSessionStatus.Running"] = "Running",

        ["Enum.ReconciliationSessionStatus.Completed"] = "Completed",

        ["Enum.ReconciliationSessionStatus.Archived"] = "Archived",

        ["Enum.ReferenceListImportMode.Supplement"] = "Supplement",

        ["Enum.ReferenceListImportMode.Replace"] = "Replace",

        ["Export Quantity Balance"] = "Export Quantity Balance",

        ["Export Inbound Documents"] = "Export Inbound Documents",

        ["Export Quantity Transactions"] = "Export Quantity Transactions",

        ["Export Adjustment Documents"] = "Export Adjustment Documents",

        ["Export Inventory Check Documents"] = "Export Inventory Check Documents",

        ["Export Move Documents"] = "Export Move Documents",

        ["Export Repair Documents"] = "Export Repair Documents",

        ["Export Borrow Documents"] = "Export Borrow Documents",

        ["Export Warehouse Structure"] = "Export Warehouse Structure",

        ["Export Item Catalog"] = "Export Item Catalog"

    };



    private static readonly Dictionary<string, string> ZhSupplement = new()

    {
        ["SystemError.UserMessage"] = "系统发生错误。错误代码：{0}。请联系 TE/IT 获取支持。",
        ["Error Management"] = "错误管理",
        ["SuperAdmin Password"] = "SuperAdmin 密码",
        ["Unlock Error Management"] = "解锁错误管理",
        ["System Errors"] = "系统错误",
        ["Error Code"] = "错误代码",
        ["Module"] = "模块",
        ["Request Path"] = "请求路径",
        ["HTTP Method"] = "HTTP 方法",
        ["Error message"] = "错误消息",
        ["Detail"] = "详情",
        ["Resolved by"] = "处理人",
        ["Client IP"] = "客户端 IP",
        ["Browser"] = "浏览器",
        ["Resolved"] = "已处理",
        ["Unresolved"] = "未处理",
        ["Mark resolved"] = "标记为已处理",
        ["Mark this system error as resolved?"] = "将此系统错误标记为已处理？",
        ["Resolution notes"] = "处理备注",
        ["Stack Trace"] = "堆栈跟踪",
        ["Payload JSON"] = "Payload JSON",
        ["Inner Exception"] = "内部异常",
        ["SuperAdmin password is invalid or not configured."] = "SuperAdmin 密码无效或尚未配置。",
        ["Error log not found."] = "未找到错误日志。",

        ["Quantity Inventory"] = "数量库存",

        ["Quantity Stock"] = "数量库存",

        ["Quantity Transactions"] = "数量库存记录",

        ["Quantity Operation"] = "数量库存操作",

        ["Receive"] = "入库",

        ["Issue"] = "出库",

        ["Adjust"] = "调整",

        ["SN"] = "SN",

        ["Qty"] = "数量",

        ["Quantity"] = "数量",

        ["Quantity inventory posted."] = "数量库存已过账。",

        ["Quantity must be greater than zero."] = "数量必须大于 0。",

        ["SN is required."] = "必须输入 SN。",

        ["Access denied for selected warehouse."] = "无权操作所选仓库。",

        ["Permission denied for this warehouse."] = "无权访问此仓库。",

        ["Permission denied for inventory check warehouse."] = "无权操作此盘点仓库。",

        ["Warehouse is invalid."] = "仓库无效。",

        ["Current role cannot manage this warehouse."] = "当前角色不能管理此仓库。",

        ["Current role cannot use this import type."] = "当前角色不能使用此导入类型。",

        ["Bin code already exists in warehouse."] = "库位编码已在仓库中存在。",

        ["User name already exists."] = "用户名已存在。",

        ["You cannot remove your own admin access."] = "不能移除自己的管理员权限。",

        ["You cannot deactivate your own account."] = "不能停用自己的账号。",

        ["You cannot hard delete your own account."] = "不能硬删除自己的账号。",

        ["No file uploaded."] = "未上传文件。",

        ["File is empty."] = "文件为空。",

        ["Category code already exists."] = "物料组编码已存在。",

        ["Party type is invalid."] = "外部对象类型无效。",

        ["Party code already exists."] = "外部对象编码已存在。",

        ["Item code already exists."] = "物料编码已存在。",

        ["Category is invalid."] = "物料组无效。",

        ["Cannot hard delete this record because it is referenced by operational data. Use soft delete instead."] = "该记录已被业务数据引用，不能硬删除。请改用停用。",

        ["Target external location is required for borrowed item."] = "借出物料必须填写外部位置。",

        ["Borrowed item must belong to the selected warehouse."] = "借出物料必须属于所选仓库。",

        ["DocumentNo {0} already exists."] = "单据号 {0} 已存在。",

        ["Insufficient quantity for item {0} SN {1}."] = "物料 {0}，SN {1} 数量不足。",

        ["Enum.QuantityInventoryDocumentType.Receive"] = "入库",

        ["Enum.QuantityInventoryDocumentType.Issue"] = "出库",

        ["Enum.QuantityInventoryDocumentType.Adjust"] = "调整",

        ["AuditEntity.QuantityInventoryDocument"] = "数量库存单",

        ["AuditAction.Receive"] = "数量入库",

        ["AuditAction.Issue"] = "数量出库",

        ["AuditAction.Adjust"] = "数量调整",

        ["Enum.ItemTrackingType.LocationTracked"] = "按位置追踪",

        ["Enum.ItemTrackingType.QuantityOnly"] = "仅数量管理",

        ["Enum.ExternalPartyType.Receiver"] = "接收方",

        ["Enum.ReconciliationResultType.Matched"] = "匹配",

        ["Enum.ReconciliationResultType.ERPOnly"] = "仅ERP存在",

        ["Enum.ReconciliationResultType.RefOnly"] = "仅参考列表存在",

        ["Enum.ReconciliationSessionStatus.Draft"] = "草稿",

        ["Enum.ReconciliationSessionStatus.Running"] = "运行中",

        ["Enum.ReconciliationSessionStatus.Completed"] = "已完成",

        ["Enum.ReconciliationSessionStatus.Archived"] = "已归档",

        ["Enum.ReferenceListImportMode.Supplement"] = "补充",

        ["Enum.ReferenceListImportMode.Replace"] = "替换",

        ["Export Quantity Balance"] = "导出数量库存",

        ["Export Inbound Documents"] = "导出入库单",

        ["Export Quantity Transactions"] = "导出数量事务",

        ["Export Adjustment Documents"] = "导出调整单",

        ["Export Inventory Check Documents"] = "导出盘点单",

        ["Export Move Documents"] = "导出移库单",

        ["Export Repair Documents"] = "导出维修单",

        ["Export Borrow Documents"] = "导出借用单",

        ["Export Warehouse Structure"] = "导出仓库结构",

        ["Export Item Catalog"] = "导出物料目录"

    };



    private static readonly Dictionary<string, string> Vi = new()

    {

        ["Dashboard"] = "Bảng điều khiển",

        ["Tracking"] = "Tra cứu hàng",

        ["Inventory List"] = "Danh sách tồn kho",

        ["Inbound Create"] = "Nhập kho",

        ["Move Location"] = "Chuyển vị trí",

        ["Adjustment"] = "Điều chỉnh",

        ["Inventory Check"] = "Kiểm kê",

        ["Repair Send"] = "Gửi sửa chữa",

        ["Repair Receive"] = "Nhận sửa chữa",

        ["Borrow Lend"] = "Cho mượn",

        ["Borrow Return"] = "Nhận trả",

        ["Warehouse Structure"] = "Cấu trúc kho",

        ["Master Data"] = "Dữ liệu danh mục",

        ["Import Excel"] = "Nhập Excel",

        ["Reports / Audit"] = "Báo cáo / Nhật ký",

        ["Search"] = "Tìm kiếm",

        ["Warehouse"] = "Kho",

        ["Status"] = "Trạng thái",

        ["sessionStatus"] = "Trạng thái",

        ["Category"] = "Nhóm hàng",

        ["Keyword"] = "Từ khóa",

        ["Timeline"] = "Lịch sử",

        ["Related Documents"] = "Chứng từ liên quan",

        ["No data"] = "Không có dữ liệu",

        ["Save & Post"] = "Lưu & ghi sổ",

        ["Line Items"] = "Dòng hàng",

        ["Load Report"] = "Tải báo cáo",

        ["Loading..."] = "Đang tải...",

        ["Access denied for current role."] = "Tài khoản hiện tại không có quyền truy cập.",

        ["Scan or enter an item to see current status and location."] = "Quét hoặc nhập mã hàng/serial/barcode để xem trạng thái và vị trí hiện tại.",

        ["Keyword is required."] = "Bắt buộc nhập từ khóa.",

        ["No quick action available for current role."] = "Vai trò hiện tại không có thao tác nhanh.",

        ["No quick action available for current status."] = "Trạng thái hiện tại không có thao tác nhanh.",

        ["Active"] = "Hoạt động",

        ["Inactive"] = "Ngưng hoạt động",

        ["Yes"] = "Có",

        ["No"] = "Không",

        ["Upload"] = "Tải lên",

        ["Validate"] = "Kiểm tra",

        ["Review"] = "Rà soát",

        ["Export Inventory"] = "Xuất tồn kho",

        ["Export History"] = "Xuất lịch sử",

        ["Export Audit"] = "Xuất nhật ký",

        ["From Date"] = "Từ ngày",

        ["To Date"] = "Đến ngày",

        ["Import Type"] = "Loại nhập",

        ["Select File"] = "Chọn file",

        ["Template"] = "Tải mẫu",

        ["Upload File"] = "Tải file",

        ["Confirm Import"] = "Xác nhận nhập",

        ["New Master Record"] = "Thêm dữ liệu",

        ["New Warehouse / Bin"] = "Thêm kho / vị trí",

        ["Import Batches"] = "File import",

        ["Validation Result"] = "Kết quả kiểm tra",

        ["Actions"] = "Thao tác",

        ["Edit"] = "Sửa",

        ["Soft Delete"] = "Ngưng dùng",

        ["Restore"] = "Khôi phục",

        ["Hard Delete"] = "Xóa hẳn",

        ["Create User"] = "Thêm tài khoản",

        ["Users"] = "Tài khoản",

        ["Roles"] = "Vai trò",

        ["Audit Log"] = "Nhật ký hệ thống",

        ["Warehouse Permissions"] = "Quyền theo kho",

        ["Display Name"] = "Tên hiển thị",

        ["User Name"] = "Tên đăng nhập",

        ["Password"] = "Mật khẩu",

        ["Preferred Language"] = "Ngôn ngữ mặc định",

        ["Assigned Warehouses"] = "Kho được phân quyền",

        ["Save"] = "Lưu",

        ["Saved"] = "Đã lưu",

        ["Record deactivated."] = "Đã ngưng dùng dữ liệu.",

        ["Record restored."] = "Đã khôi phục dữ liệu.",

        ["Record deleted."] = "Đã xóa dữ liệu.",

        ["All"] = "Tất cả",

        ["Load"] = "Tải",

        ["Entity"] = "Loại dữ liệu",

        ["Items"] = "Vật tư",

        ["Categories"] = "Nhóm hàng",

        ["External Parties"] = "Đối tác",

        ["Type"] = "Loại",

        ["Name"] = "Tên",

        ["Code"] = "Mã",

        ["Location Hierarchy"] = "Cây vị trí",

        ["Bin code"] = "Mã bin",

        ["Default CompanyCode"] = "FOXCONN/FII",

        ["Default CompanyName"] = "TẬP ĐOÀN FOXCONN",

        ["Default BranchCode"] = "FUYU",

        ["Default BranchName"] = "CÔNG TY TNHH CÔNG NGHỆ CHÍNH XÁC FUYU",

        ["Company Code"] = "Mã công ty",

        ["Company Name"] = "Tên công ty",

        ["Branch Code"] = "Mã chi nhánh",

        ["Branch Name"] = "Tên chi nhánh",

        ["Warehouse Code"] = "Mã kho",

        ["Warehouse Name"] = "Tên kho",

        ["Zone Code"] = "Mã khu",

        ["Zone Name"] = "Tên khu",

        ["Rack Code"] = "Mã kệ",

        ["Rack Name"] = "Tên kệ",

        ["Shelf Code"] = "Mã tầng",

        ["Shelf Name"] = "Tên tầng",

        ["Bin Code"] = "Mã bin",

        ["Category Code"] = "Mã nhóm hàng",

        ["Party Code"] = "Mã đối tác",

        ["Contact Name"] = "Người liên hệ",

        ["Phone"] = "Điện thoại",

        ["Email"] = "Email",

        ["Item Code"] = "Mã vật tư",

        ["Default Name"] = "Tên mặc định",

        ["Unit Code"] = "Mã đơn vị",

        ["Unit Name"] = "Tên đơn vị",


        ["Are you sure you want to finalize this inventory check session? Missing items will be calculated and adjustments generated if needed."] = "Bạn có chắc chắn muốn hoàn tất phiên kiểm kê này không? Các vật tư thiếu sẽ được tính toán và tạo phiếu điều chỉnh nếu cần.",

        ["Serial managed"] = "Quản lý theo serial",

        ["This permanently removes unused trash data only."] = "Chỉ xóa hẳn dữ liệu rác chưa phát sinh nghiệp vụ.",

        ["Global search item / serial / barcode"] = "Tìm mã hàng / serial / barcode",

        ["Inventory Enterprise"] = "Quản lý kho doanh nghiệp",
        ["B34G Warehouse"] = "Kho B34G",
        ["Warehouse Operations Portal"] = "Cổng vận hành kho",

        ["Refresh"] = "Làm mới",

        ["Total items"] = "Tổng số hàng",

        ["All item instances in scope"] = "Tổng mặt hàng trong phạm vi quyền",

        ["In stock"] = "Trong kho",

        ["Available for operations"] = "Có thể thao tác",

        ["Repairing"] = "Đang sửa",

        ["Items at repair vendors"] = "Hàng tại đơn vị sửa chữa",

        ["Lent out"] = "Đang cho mượn",

        ["Borrowed by external parties"] = "Hàng đang ở người mượn",

        ["Overdue return"] = "Quá hạn trả",

        ["Borrow lines past due date"] = "Dòng mượn đã quá hạn",

        ["Damaged or lost"] = "Hỏng, mất hoặc báo phế",

        ["Exception status"] = "Trạng thái ngoại lệ",

        ["Stock by Warehouse"] = "Tồn theo kho",

        ["Movement Trend"] = "Xu hướng phát sinh",

        ["Search results"] = "Kết quả tìm kiếm",

        ["Scan or enter item code / serial / barcode"] = "Quét hoặc nhập mã hàng / serial / barcode",

        ["Scanner input, item code, serial number and barcode are resolved by /Tracking/Search."] = "Dữ liệu quét, mã hàng, serial và barcode được tra cứu từ /Tracking/Search.",

        ["Item"] = "PN",/*Hàng hóa",*/

        ["Serial / Barcode"] = "Serial / Barcode",

        ["Current Location"] = "Vị trí hiện tại",

        ["Holder"] = "Người giữ",

        ["Reference"] = "Tham chiếu",

        ["Current Status"] = "Trạng thái hiện tại",

        ["Updated At"] = "Cập nhật lúc",

        ["Updated"] = "Cập nhật",

        ["by"] = "bởi",

        ["Stock Balance"] = "Số dư tồn",

        ["Item Preview"] = "Xem nhanh hàng hóa",

        ["Open Tracking"] = "Mở tra cứu",

        ["New Inbound"] = "Tạo phiếu nhập",

        ["Last Updated"] = "Cập nhật cuối",

        ["Server-side /Inventory/List"] = "Dữ liệu từ /Inventory/List",

        ["Header"] = "Thông tin chung",

        ["Add line"] = "Thêm dòng",

        ["Post Summary"] = "Tóm tắt ghi sổ",

        ["CreatedBy"] = "Người tạo",

        ["ApprovedBy"] = "Người xét duyệt",

        ["Posted"] = "Ghi sổ",

        ["Immediately"] = "Ngay lập tức",

        ["History"] = "Lịch sử",

        ["Append only"] = "Chỉ ghi thêm",

        ["Confirm Save & Post"] = "Xác nhận lưu & ghi sổ",

        ["This operation will be posted immediately."] = "Nghiệp vụ sẽ được ghi sổ ngay.",

        ["Backend will validate role, warehouse scope and state transition."] = "Backend sẽ kiểm tra quyền, phạm vi kho và trạng thái hợp lệ.",

        ["Operation"] = "Nghiệp vụ",

        ["Rows"] = "Số dòng",

        ["Cancel"] = "Hủy",

        ["Confirm"] = "Xác nhận",

        ["Document posted"] = "Chứng từ đã ghi sổ",

        ["Welcome to WMS"] = "Chào mừng đến WMS",

        ["Mark read"] = "Đã đọc",

        ["Borrow Document No"] = "Mã đầu đơn",

        ["Borrow Date"] = "Ngày mượn",

        ["Borrow Department"] = "Bộ phận mượn",

        ["Return Department"] = "Bộ phận trả",

        ["Approver"] = "Người xét duyệt",

        ["Borrower Phone"] = "Số điện thoại",

        ["Department Owner"] = "Chủ quản bộ phận kho",

        ["Borrowing Department"] = "Bên mượn",

        ["Returning Department"] = "Bên trả",

        ["Receiving Department"] = "Bên nhập kho",

        ["Warehouse Department"] = "Bên kho",

        ["Borrower"] = "Người mượn",

        ["Returner"] = "Người trả hàng",

        ["RepairSenderCode"] = "Mã người gửi sửa chữa",

        ["RepairSenderName"] = "Tên người gửi sửa chữa",

        ["Repair Sender Information"] = "Thông tin người gửi sửa chữa",

        ["ReturnerCode"] = "Mã người trả hàng",

        ["ReturnerName"] = "Tên người trả hàng",

        ["Borrow Warehouse"] = "Kho cho mượn",

        ["Due Date"] = "Ngày hẹn trả",

        ["Purpose"] = "Mục đích",

        ["Documents"] = "Danh sách phiếu",

        ["Document No"] = "Mã đầu đơn",

        ["Document Date"] = "Ngày chứng từ",

        ["Party"] = "Đối tượng",

        ["Lines"] = "Số dòng",

        ["View Detail"] = "Xem chi tiết",

        ["Open"] = "Đang mở",

        ["Returned"] = "Đã trả",

        ["Existing Warehouse"] = "Kho hiện có",

        ["Select an existing warehouse to add another bin under it, or leave empty to create a new warehouse hierarchy."] = "Chọn kho hiện có để thêm vị trí mới trong kho đó, hoặc để trống để tạo cây kho mới.",

        ["Role and warehouse scoped"] = "Theo quyền vai trò và kho",

        ["Condition"] = "Tình trạng",

        ["Note"] = "Ghi chú",

        ["Item Instance"] = "Mặt hàng cụ thể",

        ["Target Bin"] = "Bin đích",

        ["Actual Bin"] = "Bin thực tế",

        ["New Serial"] = "Serial mới",

        ["Target Bin / Note"] = "Bin đích / Ghi chú",

        ["Reason"] = "Lý do",

        ["Result"] = "Kết quả",

        ["Zone"] = "Khu",

        ["Rack"] = "Kệ",

        ["Shelf"] = "Tầng",

        ["Bin"] = "Vị trí",

        ["Full Path"] = "Đường dẫn đầy đủ",

        ["bins"] = "vị trí",

        ["Structure Mode"] = "Kiểu tạo cấu trúc",

        ["Add position to existing warehouse"] = "Thêm vị trí vào kho hiện có",

        ["Create new warehouse hierarchy"] = "Tạo cây kho mới",

        ["Warehouse company, branch and code are inherited from the selected warehouse."] = "Thông tin công ty, chi nhánh và mã kho được lấy theo kho đã chọn.",

        ["Item is required."] = "Bắt buộc chọn vật tư.",

        ["Bin is required."] = "Bắt buộc chọn vị trí.",

        ["Already selected"] = "Đã chọn",

        ["Already selected in another row"] = "Đã được chọn ở dòng khác",

        ["Each item can only appear once per inbound document."] = "Mỗi vật tư chỉ được xuất hiện một lần trong phiếu nhập.",

        ["Each item can only appear once per document."] = "Mỗi mặt hàng chỉ được xuất hiện một lần trong một phiếu.",

        ["Each bin can only appear once per inbound document."] = "Mỗi vị trí chỉ được xuất hiện một lần trong phiếu nhập.",

        ["Each target bin can only appear once per document."] = "Mỗi bin đích chỉ được xuất hiện một lần trong một phiếu.",

        ["Serial is duplicated in this inbound document."] = "Serial bị trùng trong phiếu nhập này.",

        ["Barcode is duplicated in this inbound document."] = "Barcode bị trùng trong phiếu nhập này.",

        ["User"] = "Người dùng",

        ["Audit Action"] = "Hành động nhật ký",

        ["Audit Entity"] = "Đối tượng nhật ký",

        ["Reference No"] = "Mã đầu đơn",

        ["Time"] = "Thời gian",

        ["Action"] = "Hành động",

        ["Summary Warehouse"] = "Kho tổng hợp",

        ["Stock by Status"] = "Tồn theo trạng thái",

        ["Movement by Operation"] = "Phát sinh theo nghiệp vụ",

        ["Days"] = "Số ngày",

        ["Last 7 days"] = "7 ngày gần nhất",

        ["Last 14 days"] = "14 ngày gần nhất",

        ["Last 30 days"] = "30 ngày gần nhất",

        ["Last 90 days"] = "90 ngày gần nhất",

        ["System will validate role, warehouse scope and state transition."] = "Hệ thống sẽ kiểm tra quyền, phạm vi kho và trạng thái nghiệp vụ hợp lệ.",

        ["The same item can be entered on multiple lines when each serial/barcode and bin is unique."] = "Có thể nhập cùng một vật tư trên nhiều dòng nếu mỗi serial/barcode và vị trí là duy nhất.",

        ["Attachment metadata is persisted by Attachment table when upload is enabled."] = "Thông tin tệp đính kèm được lưu vào bảng Attachment khi bật upload.",

        ["Please correct the highlighted data before posting."] = "Vui lòng sửa các dữ liệu đang được đánh dấu trước khi ghi sổ.",

        ["Row"] = "Dòng",

        ["System"] = "Hệ thống",

        ["Serial"] = "Serial",

        ["Barcode"] = "Barcode",

        ["Page"] = "Trang",

        ["rows"] = "dòng",

        ["Inventory Preview"] = "Xem trước tồn kho",

        ["Movement History"] = "Lịch sử phát sinh",

        ["Serial-managed"] = "Quản lý serial",

        ["Active/inactive from DB"] = "Trạng thái hoạt động/ngưng ",

        ["Supplier, borrower, repair vendor"] = "Đối tác",

        ["Item and resource translations"] = "Bản dịch vật tư và tài nguyên giao diện",

        ["Serial Tracking"] = "Theo dõi serial",

        ["Language Coverage"] = "Ngôn ngữ đã có",

        ["Add InStock or Damaged items. Save & Post changes status Repairing, location RepairVendor, writes history."] = "Thêm hàng trong kho hoặc hàng hư hỏng. Lưu & ghi sổ sẽ chuyển trạng thái sang đang sửa, vị trí là đơn vị sửa chữa và ghi lịch sử.",

        ["At least one line is required."] = "Cần ít nhất một dòng.",

        ["Current state is calculated from CurrentItemLocation and StockBalance in SQL Server."] = "Trạng thái hiện tại được tính từ CurrentItemLocation và StockBalance trong SQL Server.",

        ["Source"] = "Nguồn",

        ["Inbound Date"] = "Ngày nhập",

        ["Document No Auto"] = "Mã chứng từ tự động",

        ["Move Date"] = "Ngày chuyển",

        ["Adjustment Date"] = "Ngày điều chỉnh",

        ["Session Date"] = "Ngày kiểm kê",

        ["Count Method"] = "Phương pháp đếm",

        ["countMethod"] = "Phương pháp đếm",

        ["Responsible Staff"] = "Nhân sự phụ trách",

        ["Vendor"] = "Đơn vị sửa chữa",

        ["Send Date"] = "Ngày gửi",

        ["Expected Return"] = "Ngày dự kiến trả",

        ["Repair Document"] = "Phiếu sửa chữa",

        ["Return Warehouse"] = "Kho nhận trả",

        ["Result Note"] = "Ghi chú kết quả",

        ["Borrow Document"] = "Phiếu mượn",

        ["Return Date"] = "Ngày trả",

        ["inbound"] = "Nhập kho",

        ["move"] = "Chuyển vị trí",

        ["adjustment"] = "Điều chỉnh",

        ["inventory-check"] = "Kiểm kê",

        ["repair-send"] = "Gửi sửa chữa",

        ["repair-receive"] = "Nhận sửa chữa",

        ["borrow-lend"] = "Cho mượn",

        ["borrow-return"] = "Nhận trả",

        ["Home"] = "Trang chủ",

        ["Inventory"] = "Kho",

        ["Reports"] = "Báo cáo",

        ["Borrow"] = "Mượn",

        ["Return"] = "Nhận trả",

        ["Send To Repair"] = "Gửi sửa chữa",

        ["Receive From Repair"] = "Nhận sửa chữa",

        ["Line grid: item, serial/barcode, qty, bin location, condition, note. Save & Post creates stock/location/history."] = "Dòng hàng gồm hàng hóa, serial/barcode, số lượng, bin, tình trạng, ghi chú. Lưu & ghi sổ sẽ tạo tồn kho, vị trí và lịch sử.",

        ["Only InStock allowed. Save updates CurrentItemLocation and ItemMovementHistory."] = "Chỉ cho phép hàng trong kho. Lưu sẽ cập nhật vị trí hiện tại và lịch sử.",

        ["Used to correct stock, status or location with mandatory reason. Save & Post creates adjustment transaction and append-only history."] = "Dùng để điều chỉnh tồn, trạng thái hoặc vị trí và bắt buộc nhập lý do. Lưu & ghi sổ tạo giao dịch điều chỉnh và lịch sử chỉ ghi thêm.",

        ["Scan or import actual count, compare with system stock, then generate adjustment when approved by current user."] = "Quét hoặc import số kiểm kê thực tế, so sánh với hệ thống và ghi nhận kết quả kiểm kê.",

        ["Add only InStock items. Save & Post changes status Repairing, location RepairVendor, writes history."] = "Chỉ thêm hàng trong kho. Lưu & ghi sổ chuyển sang đang sửa, vị trí đơn vị sửa chữa và ghi lịch sử.",

        ["Add InStock or Damaged items. Each line requires a destination bin. Save & Post changes status Repairing and writes history."] = "Thêm hàng trong kho hoặc hàng hư hỏng. Mỗi dòng bắt buộc có bin đích. Lưu & ghi sổ chuyển trạng thái sang đang sửa và ghi lịch sử.",

        ["Add InStock or Damaged items. Each line requires an external destination outside warehouse. Save & Post changes status Repairing and writes history."] = "Thêm hàng trong kho hoặc hàng hư hỏng. Mỗi dòng bắt buộc nhập vị trí ngoài kho. Lưu & ghi sổ chuyển trạng thái sang đang sửa chữa và ghi lịch sử.",

        ["Success/Replaced requires target bin. Replaced requires unique new serial and old-new relationship."] = "Thành công/thay thế cần bin nhận về. Thay thế cần serial mới duy nhất và quan hệ cũ-mới.",

        ["Each repaired item requires its own destination bin. Replaced requires unique new serial and old-new relationship."] = "Mỗi hàng nhận sửa chữa cần một bin đích riêng. Hàng thay thế cần serial mới duy nhất và quan hệ cũ-mới.",

        ["Add only InStock items. Save & Post changes status LentOut and location Borrower."] = "Chỉ thêm hàng trong kho. Lưu & ghi sổ chuyển sang đã cho mượn và vị trí người mượn.",

        ["Select a borrow warehouse first, then choose in-stock items and destination bins for each line."] = "Chọn kho cho mượn trước, sau đó chọn hàng trong kho và bin đích cho từng dòng.",

        ["Select a borrow warehouse first, then choose in-stock items and external destination for each line."] = "Chọn kho cho mượn trước, sau đó chọn hàng trong kho và nhập vị trí ngoài kho cho từng dòng.",

        ["Supports partial return. Normal requires target bin. Damaged/Lost controls target status."] = "Hỗ trợ trả một phần. Bình thường cần bin nhận về. Hỏng/mất quyết định trạng thái sau trả.",

        ["Inbound posted."] = "Đã ghi sổ nhập kho.",

        ["Borrow lend posted."] = "Đã ghi sổ phiếu mượn.",

        ["Borrow return posted."] = "Đã ghi sổ phiếu trả.",

        ["Move posted."] = "Đã ghi sổ chuyển vị trí.",

        ["Repair send posted."] = "Đã ghi sổ gửi sửa chữa.",

        ["Repair receive posted."] = "Đã ghi sổ nhận sửa chữa.",

        ["Adjustment posted."] = "Đã ghi sổ điều chỉnh.",

        ["Inventory check posted."] = "Đã ghi sổ kiểm kê.",

        ["Request failed."] = "Yêu cầu thất bại.",

       

        ["Company"] = "Công ty",

        ["Branch"] = "Chi nhánh",

        ["BinLocation"] = "Vị trí bin",

        ["Move"] = "Chuyển vị trí",

        ["Repair"] = "Sửa chữa",

        ["Lend"] = "Cho mượn",

        ["history rows"] = "dòng lịch sử",

        ["Vietnamese"] = "Tiếng Việt",

        ["English"] = "Tiếng Anh",

        ["Chinese"] = "Tiếng Trung",

        ["Audit Activity"] = "Hoạt động nhật ký",

        ["Audit Object Type"] = "Loại đối tượng nhật ký",

        

        ["At least one item is required."] = "Cần chọn ít nhất một mặt hàng.",

        ["At least one inbound line is required."] = "Cần ít nhất một dòng nhập kho.",

        ["This implementation tracks one item instance per line; quantity must be 1."] = "Hệ thống theo dõi một con hàng trên mỗi dòng; số lượng phải bằng 1.",

        ["Item {0} is invalid."] = "Vật tư {0} không hợp lệ.",

        ["Item {0} requires serial number."] = "Vật tư {0} bắt buộc có serial.",

        ["Serial {0} already exists for item {1}."] = "Serial {0} đã tồn tại cho hàng hóa {1}.",

        ["Serial {0} is duplicated in this inbound document."] = "Serial {0} bị trùng trong phiếu nhập này.",

        ["Serial {0} already exists."] = "Serial {0} đã tồn tại.",

        ["Barcode {0} is duplicated in this inbound document."] = "Barcode {0} bị trùng trong phiếu nhập này.",

        ["Barcode {0} already exists."] = "Barcode {0} đã tồn tại.",

        ["Bin {0} is invalid for warehouse {1}."] = "Bin {0} không hợp lệ với kho {1}.",

        ["Bin {0} is already used in another inbound line."] = "Bin {0} đã được dùng ở dòng nhập khác.",

        ["Bin {0} already contains another active item."] = "Bin {0} đang có mặt hàng khác.",

        ["Target bin is required when item returns to stock."] = "Bắt buộc chọn bin đích khi hàng quay lại kho.",

        ["Target bin is required for every repaired item."] = "Bắt buộc chọn bin đích cho từng hàng gửi sửa chữa.",

        ["Target bin is required for borrowed item."] = "Bắt buộc chọn bin đích cho hàng cho mượn.",

        ["External Destination"] = "Vị trí ngoài kho",

        ["Target external location is required for every repaired item."] = "Bắt buộc nhập vị trí ngoài kho cho từng hàng gửi sửa chữa.",

        ["Target external location is required for borrowed item."] = "Bắt buộc nhập vị trí ngoài kho cho hàng cho mượn.",

        ["Target bin is invalid."] = "Bin đích không hợp lệ.",

        ["Target bin {0} is invalid."] = "Bin đích {0} không hợp lệ.",

        ["Target bin {0} already contains another active item."] = "Bin đích {0} đang có mặt hàng khác.",

        ["Target bin {0} is occupied by item {1} that is not moved in this document."] = "Bin đích {0} đang có mặt hàng {1} không nằm trong phiếu chuyển này.",

        ["Target bin {0} is already used in another line."] = "Bin đích {0} đã được dùng ở dòng khác.",

        ["Target bin must belong to the item's warehouse."] = "Bin đích phải thuộc kho hiện tại của mặt hàng.",

        ["Permission denied for target bin."] = "Không có quyền thao tác với bin đích.",

        ["One target bin can only receive one item. Return each repaired item to a separate bin."] = "Một bin đích chỉ nhận một con hàng. Hãy trả từng hàng sửa chữa vào bin riêng.",

        ["Borrow document number is required."] = "Bắt buộc nhập Mã đầu đơn.",

        ["Borrow warehouse is required."] = "Bắt buộc chọn kho cho mượn.",

        ["BorrowerName is required."] = "Bắt buộc nhập người mượn mượn.",

        ["Purpose is required."] = "Bắt buộc nhập mục đích mượn.",

        ["Borrow department is required."] = "Bắt buộc nhập bộ phận mượn.",

        ["Approver is required."] = "Bắt buộc nhập người xét duyệt.",

        ["Phone is required."] = "Bắt buộc nhập số điện thoại.",

        ["Department owner is required."] = "Bắt buộc nhập chủ quản bộ phận.",

        ["Borrow document number already exists."] = "Mã đầu đơn đã tồn tại.",

        ["Borrower is invalid."] = "Người mượn không hợp lệ.",

        ["Borrow document not found."] = "Không tìm thấy phiếu mượn.",

        ["Warehouse {0} not found."] = "Không tìm thấy kho {0}.",

        ["Target bin {0} not found."] = "Không tìm thấy vị trí lưu trữ {0}.",

        ["Item {0} is not included in borrow document {1}."] = "Hàng hóa {0} không có trong phiếu mượn {1}.",


        ["Inbound document selectively edited."] = "Chứng từ nhập kho đã được chỉnh sửa chọn lọc",
        ["Changing warehouse requires line-level selective mutation support."] = "Thay đổi kho yêu cầu hỗ trợ cập nhật chọn lọc ở cấp dòng",
        ["BinCode {0} does not belong to selected warehouse."] = "BinCode {0} không thuộc kho đã chọn",
        ["Replacement adjustment line edit requires explicit rebuild/recovery flow."] = "Chỉnh sửa dòng điều chỉnh thay thế yêu cầu luồng rebuild/recovery tường minh",
        ["Item {0}/{1} is duplicated in this adjustment document."] = "Vật tư {0}/{1} bị trùng trong chứng từ điều chỉnh này",
        ["Adjustment document selectively edited."] = "Chứng từ điều chỉnh đã được chỉnh sửa chọn lọc",
        ["Borrow lend selectively edited."] = "Phiếu mượn cấp phát đã được chỉnh sửa chọn lọc",
        ["Repair send selectively edited."] = "Phiếu gửi sửa chữa đã được chỉnh sửa chọn lọc",
        ["Borrow return selectively edited."] = "Phiếu trả mượn đã được chỉnh sửa chọn lọc",
        ["Repair receive selectively edited."] = "Phiếu nhận sửa chữa đã được chỉnh sửa chọn lọc",
        ["Current location for item instance {0} does not exist."] = "Vị trí hiện tại của cá thể vật tư {0} không tồn tại",
        ["Item instance {0}/{1} cannot be moved."] = "Cá thể vật tư {0}/{1} không thể di chuyển",
        ["Item instance {0}/{1} does not belong to selected warehouse."] = "Cá thể vật tư {0}/{1} không thuộc kho đã chọn",
        ["Borrow return edited and effects rebuilt."] = "Phiếu trả mượn đã được chỉnh sửa và xây dựng lại ảnh hưởng",
        ["Invalid repair send payload."] = "Dữ liệu gửi sửa chữa không hợp lệ",
        ["Invalid quantity inventory payload."] = "Dữ liệu kiểm kê số lượng không hợp lệ",
        ["Invalid borrow lend payload."] = "Dữ liệu mượn cấp phát không hợp lệ",
        ["Invalid adjustment payload."] = "Dữ liệu điều chỉnh không hợp lệ",
        ["Invalid move payload."] = "Dữ liệu chuyển kho không hợp lệ",
        ["Invalid inbound payload."] = "Dữ liệu nhập kho không hợp lệ",
        ["Move document selectively edited."] = "Chứng từ chuyển kho đã được chỉnh sửa chọn lọc",

        ["Permission denied for borrow warehouse."] = "Không có quyền thao tác với kho cho mượn.",

        ["Borrowed item must belong to the selected warehouse."] = "Mặt hàng cho mượn phải thuộc kho đã chọn.",

        ["Target bin is required for returned or damaged item."] = "Bắt buộc chọn bin đích cho hàng trả về hoặc hư hỏng.",

        ["Repair vendor is invalid."] = "Đơn vị sửa chữa không hợp lệ.",

        ["Repair document not found."] = "Không tìm thấy phiếu sửa chữa.",

        ["Item {0} not found."] = "Không tìm thấy hàng hóa {0}.",

        ["Item instance {0} not found."] = "Không tìm thấy con hàng {0}.",

        ["Item instance not found."] = "Không tìm thấy con hàng .",

        ["Item instance {0} is not InStock."] = "Con hàng {0} không ở trạng thái trong kho.",

        ["Item instance {0} cannot be sent to repair."] = "Con hàng {0} không đủ điều kiện gửi sửa chữa.",

        ["Item instance {0} is not repairing."] = "Con hàng {0} không ở trạng thái đang sửa chữa.",

        ["Item instance {0}/{1} is not repairing."] = "Con hàng {0}/{1} không ở trạng thái đang sửa chữa.",

        ["Item instance {0} cannot be lent."] = "Con hàng {0} không đủ điều kiện cho mượn.",

        ["Item instance {0} is not lent out."] = "Con hàng {0} không ở trạng thái đang cho mượn.",

        ["Item instance {0}/{1} is not lent out."] = "Con hàng {0}/{1} không ở trạng thái đang cho mượn.",

        ["Item instance {0} is already used in another line."] = "Con hàng {0} đã được chọn ở dòng khác.",

        ["Item instance {0}/{1} is already used in another line."] = "Con hàng {0}/{1} đã được chọn ở dòng khác.",

        ["Item instance {0}/{1} is not part of this repair document."] = "Con hàng {0}/{1} không thuộc phiếu sửa chữa này.",

        ["Item {0}/{1} cannot be sent to repair."] = "Con hàng {0}/{1} không đủ điều kiện gửi sửa chữa.",

        ["Item instance {0} is not located in a warehouse bin."] = "Con hàng {0} không nằm trong bin nội bộ của kho.",

        ["Item instance {0}/{1} is not located in a warehouse bin."] = "Con hàng {0}/{1} không nằm trong bin nội bộ của kho.",

        ["Permission denied for item instance {0}."] = "Không có quyền với con hàng {0}.",

        ["Permission denied for item {0}/{1}."] = "Không có quyền với con hàng {0}/{1}.",

        ["Permission denied for item instance {0}/{1}."] = "Không có quyền với con hàng {0}/{1}.",

        ["Adjustment reason is required."] = "Bắt buộc nhập lý do điều chỉnh.",

        ["Line adjustment reason is required."] = "Bắt buộc nhập lý do điều chỉnh cho từng dòng.",

        ["Permission denied for inbound warehouse."] = "Không có quyền nhập kho này.",

        ["Permission denied for move warehouse."] = "Không có quyền chuyển vị trí trong kho này.",

        ["Permission denied for adjustment warehouse."] = "Không có quyền điều chỉnh kho này.",

        ["Permission denied for inventory check warehouse."] = "Không có quyền kiểm kê kho này.",

        ["Count method is required."] = "Bắt buộc nhập phương pháp kiểm kê.",

        ["Responsible staff is required."] = "Bắt buộc nhập nhân sự phụ trách.",

        ["Inventory check requires at least one line."] = "Kiểm kê cần ít nhất một dòng.",

        ["Inventory check result is required."] = "Bắt buộc chọn kết quả kiểm kê.",

        ["Actual bin is required for this inventory check result."] = "Bắt buộc chọn bin thực tế cho kết quả kiểm kê này.",

        ["Actual bin {0} is invalid for warehouse {1}."] = "Bin thực tế {0} không hợp lệ với kho {1}.",

        ["Matched result requires actual bin equal to the system bin."] = "Kết quả khớp yêu cầu bin thực tế trùng với bin hệ thống.",

        ["Wrong location result requires an actual bin different from the system bin."] = "Kết quả sai vị trí yêu cầu bin thực tế khác bin hệ thống.",

        ["{0} is required."] = "Bắt buộc nhập {0}.",

        ["Category code already exists."] = "Mã nhóm hàng đã tồn tại.",

        ["Party type is invalid."] = "Loại đối tác không hợp lệ.",

        ["Party code already exists."] = "Mã đối tác đã tồn tại.",

        ["Item code already exists."] = "Mã vật tư đã tồn tại.",

        ["Category is invalid."] = "Nhóm hàng không hợp lệ.",

        ["Warehouse is invalid."] = "Kho không hợp lệ.",

        ["Bin code already exists in warehouse."] = "Mã bin đã tồn tại trong kho.",

        ["Bin code {0} already exists in warehouse {1}."] = "Mã bin {0} đã tồn tại trong kho {1}.",

        ["User name already exists."] = "Tên đăng nhập đã tồn tại.",

        ["Cannot hard delete this record because it is referenced by operational data. Use soft delete instead."] = "Không thể xóa vĩnh viễn vì dữ liệu đã được nghiệp vụ tham chiếu. Hãy ngưng sử dụng thay thế.",

        ["Export PDF"] = "Xuất PDF",

        ["Stock by Category"] = "Tồn theo nhóm hàng",

        ["Location Utilization"] = "Mức sử dụng vị trí",

        ["Borrow Overdue Aging"] = "Tuổi nợ phiếu mượn quá hạn",

        ["Search options"] = "Tìm trong danh sách",

        ["Empty"] = "Trống",

        ["Occupied by"] = "Đang có",

        ["Current location"] = "Vị trí hiện tại",

        ["Target bin is occupied; add the occupying item as another move row or choose an empty bin."] = "Bin đích đang có hàng; hãy thêm mặt hàng đang chiếm bin thành dòng chuyển khác hoặc chọn bin trống.",

        ["Target bin swap is covered by another row."] = "Dòng đổi vị trí cho bin này đã được khai báo trong phiếu.",

        ["Batch"] = "File",

        ["File"] = "Tệp",

        ["Total"] = "Tổng",

        ["Blocking"] = "Lỗi chặn",

        ["Severity"] = "Mức độ",

        ["Message"] = "Thông báo",

        ["Suggested Fix"] = "Gợi ý sửa",

        ["Stocktake schedule reminder"] = "Nhắc lịch kiểm kê",

        ["Warehouse {0} is due for quarterly stocktake."] = "Kho {0} đã đến hạn kiểm kê định kỳ theo quý. Hãy tạo phiếu kiểm kê hoặc import file kiểm kê.",

        ["SerialNumber already exists."] = "Serial đã tồn tại.",

        ["SerialNumber {0} already exists."] = "Serial {0} đã tồn tại.",

        ["SerialNumber is duplicated in this import file."] = "Serial bị trùng trong file import.",

        ["SerialNumber {0} is duplicated in this import file."] = "Serial {0} bị trùng trong file import.",

        ["Barcode {0} is duplicated in this import file."] = "Barcode {0} bị trùng trong file import.",

        ["BinCode is duplicated in this import file."] = "Mã vị trí bị trùng trong file import.",

        ["BinCode is duplicated in this document."] = "Mã vị trí bị trùng trong chứng từ.",

        ["BinCode already contains another active item."] = "Vị trí đã có mặt hàng đang hoạt động.",

        ["BinCode {0} already contains another active item."] = "Vị trí {0} đã có mặt hàng đang hoạt động.",

        ["Correct the row and upload again."] = "Sửa dòng dữ liệu rồi tải file lại.",

        ["File is required."] = "Bắt buộc chọn file.",

        ["Movement Trend shows number of stock movement events by day."] = "Xu hướng phát sinh thể hiện số nghiệp vụ dịch chuyển hàng theo từng ngày.",

        ["Only empty bins are shown for this operation."] = "Nghiệp vụ này chỉ hiển thị các bin còn trống.",

        ["Move location allows empty target bins and swap targets selected in the same document."] = "Chuyển vị trí cho phép chuyển sang bin trống hoặc đổi chỗ các mặt hàng cùng nằm trong phiếu.",

        ["Occupied bins"] = "Bin đã có hàng",

        ["Empty bins"] = "Bin còn trống",

        ["1-7 days"] = "1-7 ngày",

        ["8-30 days"] = "8-30 ngày",

        ["Over 30 days"] = "Trên 30 ngày",

        ["Attachments"] = "Tệp đính kèm",

        ["Attach invoices, handover records, repair receipts or inventory evidence when available."] = "Đính kèm hóa đơn, biên bản bàn giao, phiếu sửa chữa hoặc ảnh kiểm kê khi có.",

        ["No attachments"] = "Không có tệp đính kèm",

        ["File Name"] = "Tên tệp",

        ["Size"] = "Dung lượng",

        ["Uploaded At"] = "Thời gian tải lên",

        ["Download"] = "Tải xuống",

        ["Attachment entity is invalid."] = "Đối tượng đính kèm không hợp lệ.",

        ["File is empty."] = "Tệp đang trống.",

        ["File is too large."] = "Tệp vượt quá dung lượng cho phép.",

        ["File type is not allowed."] = "Loại tệp không được phép.",

        ["Attachment uploaded."] = "Đã tải tệp đính kèm.",

        ["Attachment upload failed."] = "Tải tệp đính kèm thất bại.",

        ["Back"] = "Quay lại",

        ["Operations"] = "Thao tác",

        ["Tracking Type"] = "Loại theo dõi",

        ["Translations"] = "Bản dịch",

        ["Transactions"] = "Giao dịch",



        ["SystemUsers"] = "Người dùng hệ thống",

        ["SystemRoles"] = "Vai trò hệ thống",

        ["UserWarehousePermissions"] = "Quyền theo kho",

        ["Notifications"] = "Thông báo",

        ["Unread Notifications"] = "Thông báo chưa đọc",

        ["Warehouse Manager"] = "Quản lý kho",

        ["Admin"] = "Quản trị viên",

        ["Warehouse Staff"] = "Nhân viên kho",

        ["Viewer"] = "Người xem",

        ["Normal"] = "Bình thường",

        ["Damaged"] = "Hư hỏng",

        ["Lost"] = "Thất lạc",

        ["Location"] = "Vị trí",

        ["Notes"] = "Ghi chú",

        ["Item Information"] = "Thông tin mặt hàng",

        ["Inventory check action required"] = "Kiểm kê cần xử lý",

        ["ItemCode is required for every check line."] = "Mỗi dòng kiểm kê bắt buộc phải có mã hàng",

        ["SerialNumber is required for every check line."] = "Mỗi dòng kiểm kê bắt buộc phải có số serial",

        ["BinCode is required for every check line."] = "Mỗi dòng kiểm kê bắt buộc phải có mã vị trí",

        ["BinCode {0} not found."] = "Mã vị trí {0} không tìm thấy.",

        ["BinCode {0} does not belong to warehouse {1}."] = "Mã vị trí {0} không thuộc kho {1}.",

        ["Item type {0} not found. Cannot create extra item."] = "Loại mặt hàng {0} không tìm thấy. Không thể tạo mặt hàng bổ sung.",

        ["Extra item found at {0}."] = "Phát hiện mặt hàng thừa tại {0}.",

        ["Found at {0} instead of expected location"] = "Phát hiện tại {0} thay vì vị trí dự kiến",

        ["Inventory check: wrong location corrected"] = "Kiểm kê: vị trí sai đã được sửa",

        ["Item not found during inventory check"] = "Kiểm kê: không tìm thấy mặt hàng",

        ["Missing: not found during inventory check"] = "Kiểm kê: mặt hàng thiếu",

        ["Item {0} with serial {1} not found."] = "Mặt hàng {0} với số serial {1} không tìm thấy.",

        ["External party {0} not found."] = "Đối tác bên ngoài {0} không tìm thấy.",

        ["Adjusted location."] = "Vị trí đã được điều chỉnh.",

        ["Repair vendor code is required."] = "Mã nhà cung cấp sửa chữa là bắt buộc.",

        ["Repair vendor {0} not found."] = "Nhà cung cấp sửa chữa {0} không tìm thấy.",

        ["Item instance {0}/{1} cannot be lent."] = "Hàng hóa {0}/{1} không thể được cho mượn.",

        ["Code"] = "Mã",

        ["Name"] = "Tên",

        ["Code-Name"] = "Mã-Tên",


        ["Current user cannot manage warehouse {0}"] = "Người dùng hiện tại không có quyền quản lý kho {0}",

        ["Import batch is valid."] = "File import hợp lệ.",

        ["Import batch has blocking errors."] = "File import có lỗi chặn.",

        ["Valid rows will be inserted into operational tables."] = "Các bản ghi hợp lệ sẽ được chèn vào các bảng nghiệp vụ.",

        ["Backend will re - validate before commit."] = "Hệ thống sẽ kiểm tra lại trước khi ghi nhận.",

        ["Import confirmed. Rows: {0}"] = "Đã xác nhận import. Số dòng: {0}",    

        ["ItemCode does not exist."] = "Mã hàng không tồn tại",

        ["SerialNumber is required for serial-managed item."] = "Số serial là bắt buộc đối với hàng quản lý theo serial",

        ["WarehouseCode does not exist."] = "Mã kho không tồn tại",

        ["Current user cannot import into this warehouse."] = "Người dùng hiện tại không có quyền nhập vào kho này",

        ["BinCode is invalid for warehouse."] = "Mã vị trí không hợp lệ cho kho này",

        ["File uploaded."] = "Tệp đã được tải lên",

        ["Import batch not found."] = "Không tìm thấy lô import",

        ["Current role cannot use this import type."] = "Vai trò hiện tại không thể sử dụng loại import này",

        ["Unsupported import type."] = "Loại import không được hỗ trợ",

        ["Barcode is duplicated in this import file."] = "Mã barcode bị trùng trong file import",

        ["ItemCode is duplicated in this import file."] = "Mã hàng bị trùng trong file import",

        ["ItemCode {0} already exists in the system."] = "Mã hàng {0} đã tồn tại trong hệ thống",

        ["Rows per page"] = "Số dòng mỗi trang",

        ["All "] = "Tất cả",

        ["Document no is required."] = "Số chứng từ là bắt buộc.",

        ["LocationTracked"] = "Theo dõi vị trí",

        ["QuantityOnly"] = "Chỉ theo dõi số lượng",

        ["Repair Document No"] = "Số phiếu sửa chữa",

        ["Exited edit mode"] = "Đã thoát chế độ chỉnh sửa",
        ["info"] = "Thông tin",
        ["Cancel Edit"] = "Hủy",
        ["No blocking dependency found."] = "Không tìm thấy phụ thuộc chặn",
        ["Document has blocking dependencies."] = "Chứng từ có phụ thuộc chặn",
        ["Unsupported document type '{0}'."] = "Loại chứng từ '{0}' không được hỗ trợ",
        ["Document not found."] = "Không tìm thấy chứng từ",
        ["Rebuild"] = "Xây dựng lại",
        ["Rebuild Effects"] = "Xây dựng lại ảnh hưởng",
        ["Reverse"] = "Đảo ngược",
        ["Delete"] = "Xóa",
        ["Document effects reversed before delete."] = "Ảnh hưởng của chứng từ đã được đảo ngược trước khi xóa",
        ["Document deleted."] = "Đã xóa chứng từ",
        ["Invalid borrow return payload."] = "Dữ liệu trả mượn không hợp lệ",
        ["Document edited and effects rebuilt."] = "Đã chỉnh sửa chứng từ và xây dựng lại ảnh hưởng",
        ["Document updated."] = "Đã cập nhật chứng từ",
        ["Borrow return updated."] = "Đã cập nhật phiếu trả mượn",
        ["Repair receive updated."] = "Đã cập nhật phiếu nhận sửa chữa",
        ["Dependency Warning"] = "Cảnh báo phụ thuộc",
        ["Review dependency impact before continuing."] = "Xem xét ảnh hưởng phụ thuộc trước khi tiếp tục",
        ["Close"] = "Đóng",
        ["Blocked reason"] = "Lý do bị chặn",
        ["Item instance {0} has downstream operations."] = "Cá thể vật tư {0} có nghiệp vụ phát sinh phía sau",
        ["Item instance {0} has downstream operations after {1}."] = "Cá thể vật tư {0} có nghiệp vụ phát sinh phía sau sau bước {1}",
        ["Quantity item {0}/{1} has later quantity transactions."] = "Mã số lượng {0}/{1} có giao dịch tồn phát sinh sau đó",
        ["Current persisted payload will be replayed to rebuild effects."] = "Dữ liệu đã lưu hiện tại sẽ được phát lại để xây dựng lại ảnh hưởng",
        ["This will reverse and replay the current document effects."] = "Thao tác này sẽ đảo ngược và phát lại ảnh hưởng của chứng từ hiện tại",
        ["Document will be reversed and deleted transactionally."] = "Chứng từ sẽ được đảo ngược và xóa trong cùng giao dịch",
        ["This document will be reversed and deleted."] = "Chứng từ này sẽ được đảo ngược và xóa",
        ["Document Type"] = "Loại chứng từ",
        ["Editing document"] = "Đang chỉnh sửa chứng từ",
        ["Save Changes"] = "Lưu",
        ["Inbound document not found."] = "Không tìm thấy phiếu nhập kho",
        ["Move document not found."] = "Không tìm thấy phiếu chuyển vị trí",
        ["Adjustment document not found."] = "Không tìm thấy phiếu điều chỉnh",
        ["Quantity inventory document not found."] = "Không tìm thấy phiếu tồn kho số lượng",
        ["Borrow document has no return effects to delete."] = "Phiếu mượn không có ảnh hưởng trả mượn để xóa",
        ["Repair document has no receive effects to delete."] = "Phiếu sửa chữa không có ảnh hưởng nhận sửa chữa để xóa",
        ["Cannot delete borrow lend after return has been posted. Delete borrow return first."] = "Không thể xóa phiếu cho mượn sau khi đã ghi nhận trả mượn. Hãy xóa phiếu trả mượn trước",
        ["Cannot delete repair send after repair receive has been posted. Delete repair receive first."] = "Không thể xóa phiếu gửi sửa chữa sau khi đã ghi nhận nhận sửa chữa. Hãy xóa phiếu nhận sửa chữa trước",
        ["Cannot edit or delete borrow lend after return has been posted. Delete borrow return first."] = "Không thể sửa hoặc xóa phiếu cho mượn sau khi đã ghi nhận trả mượn. Hãy xóa phiếu trả mượn trước",
        ["Cannot edit or delete repair send after repair receive has been posted. Delete repair receive first."] = "Không thể sửa hoặc xóa phiếu gửi sửa chữa sau khi đã ghi nhận nhận sửa chữa. Hãy xóa phiếu nhận sửa chữa trước",
        ["Receive"] = "Nhận",
        ["Success"] = "Thành công",
        ["Quantity document edited: old document deleted and effects rebuilt from new payload."] = "Chứng từ kiểm kê số lượng đã được chỉnh sửa: chứng từ cũ đã bị xóa và ảnh hưởng được xây dựng lại từ dữ liệu mới",
        ["Normal edit no longer performs full delete/repost rebuild. Line-level changes for this document type require selective mutation support; use explicit Rebuild only for recovery/full replay."] = "Chỉnh sửa thông thường không còn thực hiện rebuild bằng cách xóa và ghi lại toàn bộ. Thay đổi ở cấp dòng cho loại chứng từ này yêu cầu hỗ trợ cập nhật chọn lọc; chỉ sử dụng Rebuild tường minh cho mục đích khôi phục hoặc phát lại toàn bộ",
        ["Document header edited without rebuilding effects."] = "Phần đầu chứng từ đã được chỉnh sửa mà không xây dựng lại ảnh hưởng",

        ["Lines appended to existing repair document."] = "Các dòng đã được thêm vào phiếu sửa chữa hiện có.",

        ["ItemMaster"] = "Danh mục vật tư",

        ["WarehouseStructure"] = "Cấu trúc kho",

        ["Inbound"] = "Nhập kho",

        ["InventoryCheck"] = "Kiểm kê",



        ["Endpoint.InventoryList"] = "Dữ liệu tồn kho",

        ["Endpoint.ReportsInventoryPreview"] = "Xem trước báo cáo tồn kho",

        ["Endpoint.ReportsHistoryPreview"] = "Xem trước lịch sử phát sinh",

        ["Endpoint.AuditLogs"] = "Nhật ký hệ thống",

        ["Endpoint.DocumentsList"] = "Danh sách chứng từ",

        ["Endpoint.WarehouseStructure"] = "Cấu trúc kho",

        ["Endpoint.MasterDataList"] = "Dữ liệu danh mục",
        ["Endpoint.SystemErrors"] = "Lỗi hệ thống",



        ["AuditAction.Inbound"] = "Nhập kho",

        ["AuditAction.MoveLocation"] = "Chuyển vị trí",

        ["AuditAction.SendToRepair"] = "Gửi sửa chữa",

        ["AuditAction.ReceiveFromRepair"] = "Nhận sửa chữa",

        ["AuditAction.BorrowLend"] = "Cho mượn",

        ["AuditAction.BorrowReturn"] = "Nhận trả",

        ["AuditAction.Adjustment"] = "Điều chỉnh",

        ["AuditAction.InventoryCheck"] = "Kiểm kê",

        ["AuditAction.ImportOperation"] = "Import dữ liệu",

        ["AuditAction.Create"] = "Tạo mới",

        ["AuditAction.Edit"] = "Chỉnh sửa",

        ["AuditAction.Rebuild"] = "Xây dựng lại",

        ["AuditAction.Reverse"] = "Đảo ngược",

        ["AuditAction.Delete"] = "Xóa",

        ["AuditAction.Update"] = "Cập nhật",

        ["AuditAction.SoftDelete"] = "Ngưng sử dụng",

        ["AuditAction.Restore"] = "Khôi phục",

        ["AuditAction.HardDelete"] = "Xóa vĩnh viễn",

        ["AuditAction.ConfirmImport"] = "Xác nhận nhập dữ liệu",

        ["AuditAction.RunReconciliation"] = "Chạy đối soát",

        ["AuditAction.ReplaceImport"] = "Thay thế nhập kho",

        ["AuditEntity.ReferenceListHeader"] = "Tiêu đề danh sách tham chiếu",

        ["AuditEntity.ReconciliationSession"] = "Phiên đối soát",

        ["AuditEntity.ItemInstance"] = "Mặt hàng cụ thể",

        ["AuditAction.InventoryCheckFinalize"] = "Kiểm kê",


        ["AuditEntity.InboundDocument"] = "Phiếu nhập kho",

        ["AuditEntity.MoveDocument"] = "Phiếu chuyển vị trí",

        ["AuditEntity.RepairDocument"] = "Phiếu sửa chữa",

        ["AuditEntity.BorrowDocument"] = "Phiếu mượn",

        ["AuditEntity.AdjustmentDocument"] = "Phiếu điều chỉnh",

        ["AuditEntity.InventoryCheckDocument"] = "Phiếu kiểm kê",

        ["AuditEntity.Item"] = "Vật tư",

        ["AuditEntity.ItemCategory"] = "Nhóm hàng",

        ["AuditEntity.ExternalParty"] = "Đối tác",

        ["AuditEntity.BinLocation"] = "Vị trí kho",

        ["AuditEntity.SystemUser"] = "Tài khoản",

        ["AuditEntity.ImportBatch"] = "File import",



        ["ImportType.ItemMaster"] = "Danh mục vật tư",

        ["ImportType.WarehouseStructure"] = "Cấu trúc kho",

        ["ImportType.Inbound"] = "Nhập kho",

        ["ImportType.InventoryCheck"] = "Kiểm kê",

        ["ImportType.RepairSend"] = "Gửi sửa chữa",

        ["ImportType.BorrowLend"] = "Cho mượn",



        ["Enum.ItemStatus.InStock"] = "Trong kho",

        ["Enum.ItemStatus.Normal"] = "Bình thường",

        ["Enum.ItemStatus.Reserved"] = "Đã giữ chỗ",

        ["Enum.ItemStatus.Repairing"] = "Đang sửa chữa",

        ["Enum.ItemStatus.LentOut"] = "Đã cho mượn",

        ["Enum.ItemStatus.Returned"] = "Đã trả",

        ["Enum.ItemStatus.Damaged"] = "Hư hỏng",

        ["Enum.ItemStatus.Lost"] = "Thất lạc",

        ["Enum.ItemStatus.Disposed"] = "Đã thanh lý",

        ["Enum.ItemStatus.InTransit"] = "Đang vận chuyển",

        ["Enum.ItemStatus.Replacement"] = "Thay thế serial",

        ["Enum.ItemStatus.Scrapped"] = "Báo phế",



        ["Enum.ItemStatus.Matched"] = "Khớp",

        ["Enum.ItemStatus.Missing"] = "Thiếu",

        ["Enum.ItemStatus.Extra"] = "Dư",

        ["Enum.ItemStatus.WrongLocation"] = "Sai vị trí",



        ["Enum.InventoryCheckLineResult.Matched"] = "Khớp",

        ["Enum.InventoryCheckLineResult.Missing"] = "Thiếu",

        ["Enum.InventoryCheckLineResult.Extra"] = "Thừa",

        ["Enum.InventoryCheckLineResult.WrongLocation"] = "Sai vị trí",

        ["Enum.InventoryCheckLineResult.Damaged"] = "Hư hỏng",



        ["Enum.RepairResult.Success"] = "Sửa thành công",

        ["Enum.RepairResult.Failed"] = "Sửa thất bại",

        ["Enum.RepairResult.Replaced"] = "Đổi hàng",



        ["Enum.BorrowReturnCondition.Normal"] = "Bình thường",

        ["Enum.BorrowReturnCondition.Damaged"] = "Hư hỏng",

        ["Enum.BorrowReturnCondition.Lost"] = "Thất lạc",

        ["Enum.BorrowReturnCondition.Scrapped"] = "Báo phế",



        ["Enum.ExternalPartyType.Supplier"] = "Nhà cung cấp",

        ["Enum.ExternalPartyType.RepairVendor"] = "Đơn vị sửa chữa",

        ["Enum.ExternalPartyType.Borrower"] = "Người mượn",

        ["Enum.ExternalPartyType.Customer"] = "Khách hàng",

        ["Enum.ExternalPartyType.Department"] = "Phòng ban",

        ["Enum.ExternalPartyType.Employee"] = "Nhân viên",

        ["Enum.ExternalPartyType.Logistics"] = "Vận chuyển",
        ["Enum.ExternalPartyType.Approver"] = "Người xét duyệt",
        ["Enum.ExternalPartyType.DepartmentOwner"] = "Chủ quản bộ phận kho",



        ["Enum.MovementActionType.Inbound"] = "Nhập kho",

        ["Enum.MovementActionType.MoveLocation"] = "Chuyển vị trí",

        ["Enum.MovementActionType.SendToRepair"] = "Gửi sửa chữa",

        ["Enum.MovementActionType.ReceiveFromRepair"] = "Nhận sửa chữa",

        ["Enum.MovementActionType.Lend"] = "Cho mượn",

        ["Enum.MovementActionType.ReturnBorrowed"] = "Nhận trả",

        ["Enum.MovementActionType.Adjustment"] = "Điều chỉnh",

        ["Enum.MovementActionType.InventoryCheck"] = "Kiểm kê",

        ["Enum.MovementActionType.ImportOpening"] = "Nhập số dư đầu kỳ",

        ["Enum.MovementActionType.Dispose"] = "Thanh lý",

        ["Enum.MovementActionType.Transfer"] = "Điều chuyển",



        ["Enum.ImportBatchStatus.Uploaded"] = "Đã tải lên",

        ["Enum.ImportBatchStatus.Validated"] = "Đã kiểm tra",

        ["Enum.ImportBatchStatus.Blocked"] = "Bị chặn",

        ["Enum.ImportBatchStatus.Confirmed"] = "Đã xác nhận",

        ["Enum.ImportBatchStatus.Failed"] = "Thất bại",



        ["Enum.ValidationSeverity.Info"] = "Thông tin",

        ["Enum.ValidationSeverity.Warning"] = "Cảnh báo",

        ["Enum.ValidationSeverity.Blocking"] = "Lỗi chặn",



        ["Enum.InventoryTransactionType.Inbound"] = "Nhập kho",

        ["Enum.InventoryTransactionType.Move"] = "Chuyển vị trí",

        ["Enum.InventoryTransactionType.RepairSend"] = "Gửi sửa",

        ["Enum.InventoryTransactionType.RepairReceive"] = "Nhận sửa",

        ["Enum.InventoryTransactionType.BorrowLend"] = "Cho mượn",

        ["Enum.InventoryTransactionType.BorrowReturn"] = "Nhận trả",

        ["Enum.InventoryTransactionType.Adjustment"] = "Điều chỉnh",

        ["Enum.InventoryTransactionType.InventoryCheck"] = "Kiểm kê",

        ["Enum.InventoryTransactionType.OpeningBalance"] = "Số dư đầu kỳ",



        ["Enum.LocationType.Warehouse"] = "Kho",

        ["Enum.LocationType.BinLocation"] = "Vị trí bin",

        ["Enum.LocationType.Supplier"] = "Nhà cung cấp",

        ["Enum.LocationType.RepairVendor"] = "Đơn vị sửa chữa",

        ["Enum.LocationType.Borrower"] = "Người mượn",

        ["Enum.LocationType.Logistics"] = "Vận chuyển",

        ["Enum.LocationType.Disposed"] = "Đã thanh lý",

        ["Enum.LocationType.Unknown"] = "Không xác định",



        ["Enum.DocumentStatus.Draft"] = "Nháp",

        ["Enum.DocumentStatus.Posted"] = "Đã ghi sổ",

        ["Enum.DocumentStatus.CancelledByAdjustment"] = "Đã hủy bằng điều chỉnh",



        ["Enum.InventoryStatus.InStock"] = "Trong kho",

        ["Enum.InventoryStatus.Normal"] = "Bình thường",

        ["Enum.InventoryStatus.Damaged"] = "Hư hỏng",

        ["Enum.InventoryStatus.Scrapped"] = "Báo phế",

        ["Enum.InventoryStatus.Replacement"] = "Thay thế serial",



        ["Enum.ItemStatusView.InStock"] = "Trong kho",

        ["Enum.ItemStatusView.Normal"] = "Bình thường",

        ["Enum.ItemStatusView.Repairing"] = "Đang sửa chữa",

        ["Enum.ItemStatusView.LentOut"] = "Đã cho mượn",

        ["Enum.ItemStatusView.Damaged"] = "Hư hỏng",

        ["Enum.ItemStatusView.Lost"] = "Thất lạc",

        ["Enum.ItemStatusView.Disposed"] = "Đã thanh lý",

        ["Enum.ItemStatusView.Replacement"] = "Thay thế serial",

        ["Enum.ItemStatusView.Scrapped"] = "Báo phế",



        ["Enum.DocumentPeriodType.Week"] = "Tuần",

        ["Enum.DocumentPeriodType.Month"] = "Tháng",

        ["Enum.DocumentPeriodType.Quarter"] = "Quý",

        ["Enum.DocumentPeriodType.Year"] = "Năm",

        ["DocumentPeriodType"] = "Chu kỳ kiểm kê",



        ["Success"] = "Thành công",

        ["Failed"] = "Thất bại",

        ["Unknown"] = "Không xác định",

        ["Scrapped"] = "Báo phế",

        ["Matched"] = "Khớp",

        ["Missing"] = "Thiếu",

        ["Extra"] = "Thừa",

        ["WrongLocation"] = "Sai vị trí",

        ["InStock"] = "Trong kho",

        ["Replaced"] = "Đổi hàng",



        ["InboundReceive"] = "Nhập kho",

        ["BorrowIssue"] = "Xuất mượn",

        ["BorrowReturn"] = "Nhận trả",

        ["RepairSend"] = "Gửi sửa chữa",

        ["RepairReceive"] = "Nhận sửa chữa",



        ["By"] = "Bởi",

        ["History & Timeline"] = "Lịch sử & Dòng thời gian",

        ["Old Status"] = "Trạng thái cũ",

        ["New Status"] = "Trạng thái mới",

        ["Old Location"] = "Vị trí cũ",

        ["New Location"] = "Vị trí mới",


        ["Only permanently delete items with no transaction history."] = "Chỉ xóa vĩnh viễn các mặt hàng chưa phát sinh nghiệp vụ.",

        ["Receiver Phone"] = "Số điện thoại người nhập",

        ["Receiver Name"] = "Tên người nhập",

        ["Receiver Code"] = "Mã người nhập",

        ["Receiver Department"] = "Bộ phận người nhập",

        ["Receiver"] = "Người nhập",

        ["Receiver Purpose"] = "Mục đích nhập hàng",

        ["Warehouse B34G"] = "Kho B34G",
        ["B34G Warehouse"] = "Kho B34G",
        ["Warehouse Operations Portal"] = "Cổng vận hành kho",


        ["Borrow document has no lend effects to delete."] = "Chứng từ mượn không có phát sinh cấp phát để xóa",
        ["Latest borrow lifecycle action is a return. Delete borrow return first"] = "Nghiệp vụ mượn gần nhất là trả. Vui lòng xóa phiếu trả mượn trước",
        ["Borrow document has no latest return effects to delete."] = "Chứng từ mượn không có phát sinh trả gần nhất để xóa",
        ["This document has no return records yet."] = "Phiếu này chưa có lần trả nào",
        ["Latest repair lifecycle action is a receive. Delete repair receive first."] = "Nghiệp vụ sửa chữa gần nhất là nhận. Vui lòng xóa phiếu nhận sửa chữa trước",
        ["Repair document has no latest receive effects to delete."] = "Chứng từ sửa chữa không có phát sinh nhận gần nhất để xóa",
        ["Quantity inventory document has no latest posting effects to delete."] = "Chứng từ kiểm kê số lượng không có phát sinh ghi sổ gần nhất để xóa",
        ["Cannot edit borrow lend after return has been posted. Delete borrow return first."] = "Không thể chỉnh sửa cấp phát mượn sau khi đã có phiếu trả. Vui lòng xóa phiếu trả trước",
        ["Cannot edit repair send after repair receive has been posted. Delete repair receive first."] = "Không thể chỉnh sửa phiếu gửi sửa chữa sau khi đã có phiếu nhận. Vui lòng xóa phiếu nhận trước",
        ["Target bin is required for repaired item."] = "Bắt buộc phải có bin đích cho vật tư đã sửa chữa",
        ["Invalid repair receive payload."] = "Dữ liệu nhận sửa chữa không hợp lệ",
        ["Repair receive edited and effects rebuilt."] = "Phiếu nhận sửa chữa đã được chỉnh sửa và xây dựng lại ảnh hưởng",
        ["Inventory preview shows the current status and location of an item instance."] = "Xem trước tồn kho hiển thị trạng thái và vị trí hiện tại của một cá thể hàng hóa.",
        ["AuditAction.SuperLogin"] = "Đăng Nhập Super Admin",
        ["SuperPassword Override Login Success"] = "Đăng nhập thành công bằng SuperPassword",
        ["AuditEntity.SystemOverride"] = "Ghi Đè Hệ Thống",
        ["SuperAdmin"] = "Super Admin",


        // ─── Document Log action keys (Repair + Adjustment) ──────────────────

        ["Adjust"] = "Điều chỉnh",

        ["Replace-Out"] = "Xuất thay thế",

        ["Replace-In"] = "Nhập thay thế",

        ["Repair Vendor"] = "Đơn vị sửa chữa",

        ["Repair Result Note"] = "Ghi chú kết quả sửa",

        ["Performed By"] = "Thực hiện bởi",

        ["Action Type"] = "Loại hành động",



        // ─── InventoryCheck session-based ────────────────────────────────────

        ["Session Status"] = "Trạng thái phiên",

        ["Draft"] = "Nháp",

        ["InProgress"] = "Đang kiểm kê",

        ["Finalized"] = "Đã hoàn tất",

        ["Create Session"] = "Tạo phiên kiểm kê",

        ["Scan Batch"] = "Quét lô hàng",

        ["Finalize Session"] = "Hoàn tất kiểm kê",

        ["Session Progress"] = "Tiến độ phiên",

        ["Total Scanned"] = "Tổng đã quét",

        ["Total Missing"] = "Tổng thiếu",

        ["Batch Matched"] = "Khớp (lô)",

        ["Batch Wrong Location"] = "Sai vị trí (lô)",

        ["Batch Extra"] = "Thừa (lô)",

        ["Batch Skipped"] = "Bỏ qua (lô)",

        ["Inventory check session created."] = "Đã tạo phiên kiểm kê.",

        ["Inventory check session has already been finalized."] = "Phiên kiểm kê đã được hoàn tất.",

        ["Inventory check document not found."] = "Không tìm thấy phiếu kiểm kê.",

        ["This inventory check session has already been finalized."] = "Phiên kiểm kê này đã được hoàn tất.",

        ["At least one scan line is required."] = "Cần ít nhất một dòng scan.",

        ["Inventory check finalized."] = "Kiểm kê đã được hoàn tất.",
        ["Sender Code"] = "Mã người gửi",
        ["Sender Name"] = "Tên người gửi",
        ["Sender Phone"] = "Số điện thoại người gửi",
        ["Adjustment Type"] = "Loại điều chỉnh",
        ["Increase"] = "Tăng",
        ["Decrease"] = "Giảm",

        ["Serial-managed items require serial numbers for tracking individual instances."] = "Các mặt hàng được quản lý theo serial yêu cầu số serial để theo dõi từng cá thể riêng lẻ",

        ["Active/inactive status is determined by the presence of an active record in the database."] = "Trạng thái kích hoạt/vô hiệu được xác định bởi sự hiện diện của một bản ghi hoạt động trong cơ sở dữ liệu.",

        ["Supplier, borrower, and repair vendor information is stored as external parties in the system."] = "Thông tin nhà cung cấp, người mượn và nhà cung cấp sửa chữa được lưu trữ như các bên ngoài trong hệ thống.",

        ["Item and resource translations are provided for multilingual support in the user interface."] = "Các bản dịch vật phẩm và tài nguyên được cung cấp để hỗ trợ đa ngôn ngữ trong giao diện người dùng.",

        ["Serial tracking allows monitoring the movement and status of individual item instances through their unique serial numbers."] = "Theo dõi serial cho phép giám sát sự di chuyển và trạng thái của từng cá thể vật phẩm thông qua số serial duy nhất của chúng.",

        ["Language coverage indicates the availability of translations for different languages in the system."] = "Phạm vi ngôn ngữ cho biết sự sẵn có của các bản dịch cho các ngôn ngữ khác nhau trong hệ thống.",

        ["Permission checks are enforced for warehouse operations to ensure users have appropriate access rights."] = "Các kiểm tra quyền được thực thi cho các nghiệp vụ kho để đảm bảo người dùng có quyền truy cập phù hợp.",

        ["Import functionality allows bulk data entry for items, warehouses, and transactions using predefined templates."] = "Chức năng nhập khẩu cho phép nhập dữ liệu hàng loạt cho các mặt hàng, kho và giao dịch bằng cách sử dụng các mẫu đã định trước.",

        ["Audit logs capture user activities and changes to critical entities for accountability and traceability."] = "Nhật ký kiểm toán ghi lại các hoạt động của người dùng và các thay đổi đối với các thực thể quan trọng để đảm bảo trách nhiệm và khả năng truy xuất.",

        ["Notifications provide alerts for important events such as stocktake schedules and document approvals."] = "Thông báo cung cấp cảnh báo cho các sự kiện quan trọng như lịch kiểm kê và phê duyệt tài liệu.",

        ["Users must have the necessary permissions for the warehouses involved in the operation."] = "Người dùng phải có quyền cần thiết cho các kho liên quan trong nghiệp vụ.",

        ["Permission checks cover actions such as inbound, move location, repair send, inventory check, and adjustment."] = "Các kiểm tra quyền bao gồm các hành động như nhập kho, chuyển vị trí, gửi sửa chữa, kiểm kê và điều chỉnh.",

        ["Permission checks are performed at both the document level and line item level to ensure comprehensive access control."] = "Các kiểm tra quyền được thực hiện ở cả cấp độ chứng từ và dòng hàng để đảm bảo kiểm soát truy cập toàn diện.",

        ["Users without the required permissions will receive error messages when attempting to perform restricted operations."] = "Người dùng không có quyền cần thiết sẽ nhận được thông báo lỗi khi cố gắng thực hiện các nghiệp vụ bị hạn chế.",

        ["Warehouse permissions are assigned to users based on their roles and responsibilities within the organization."] = "Quyền kho được gán cho người dùng dựa trên vai trò và trách nhiệm của họ trong tổ chức.",

        ["Regular reviews of user permissions are recommended to maintain security and ensure that access rights are up to date."] = "Đề xuất xem xét định kỳ quyền của người dùng để duy trì an ninh và đảm bảo quyền truy cập được cập nhật.",

        ["Inventory movement history shows the historical transactions of an item instance."] = "Lịch sử di chuyển tồn kho hiển thị các giao dịch lịch sử của một đơn vị hàng hóa.",

        ["The history includes details such as transaction type, date and time, quantity, source and destination locations, and the user who performed the transaction."] = "Lịch sử bao gồm các chi tiết như loại giao dịch, ngày và giờ, số lượng, vị trí nguồn và đích, và người dùng đã thực hiện giao dịch.",

        ["Inventory movement history provides insights into the lifecycle of an item instance, including when it was received, moved, sent for repair, lent out, returned, adjusted, or checked during inventory."] = "Lịch sử di chuyển tồn kho cung cấp cái nhìn sâu sắc về vòng đời của một đơn vị hàng hóa, bao gồm khi nào nó được nhận vào kho, di chuyển, gửi sửa chữa, cho mượn, trả lại, điều chỉnh hoặc kiểm kê.",

        ["Users can access inventory movement history through the system interface to track the status and location changes of item instances over time."] = "Người dùng có thể truy cập lịch sử di chuyển tồn kho thông qua giao diện hệ thống để theo dõi trạng thái và thay đổi vị trí của các đơn vị hàng hóa theo thời gian.",



        // ─── Reconciliation Audit module ──────────────────────────────────────

        ["Reconciliation"] = "Đối soát tài sản",

        ["recon.title"] = "Đối soát tài sản",

        ["recon.reflist"] = "Danh sách tham chiếu",

        ["recon.sessions"] = "Phiên đối soát",

        ["recon.results"] = "Kết quả",

        ["recon.newlist"] = "Tạo danh sách mới",

        ["recon.newsession"] = "Tạo phiên mới",

        ["recon.import"] = "Import dữ liệu",

        ["recon.runaudit"] = "Chạy đối soát",

        ["recon.listcode"] = "Mã danh sách",

        ["recon.listname"] = "Tên danh sách",

        ["recon.description"] = "Mô tả",

        ["recon.itemcount"] = "Số dòng",

        ["recon.importmode"] = "Chế độ import",

        ["recon.supplement"] = "Bổ sung (Upsert)",

        ["recon.replace"] = "Thay mới (Xóa và import lại)",

        ["recon.matched"] = "Khớp",

        ["recon.erponly"] = "Chỉ có trên hệ thống",

        ["recon.refonly"] = "Chỉ trong danh sách tham chiếu",

        ["recon.lentout"] = "Đang mượn",

        ["recon.resolved"] = "Đã xác định trên hệ thống",

        ["recon.sessionno"] = "Mã phiên",

        ["recon.status.draft"] = "Nháp",

        ["recon.status.running"] = "Đang chạy",

        ["recon.status.completed"] = "Hoàn tất",

        ["recon.status.archived"] = "Lưu trữ",

        ["recon.export"] = "Xuất Excel",

        ["recon.template"] = "Tải mẫu Excel",

        ["recon.allresults"] = "Tất cả kết quả",

        ["recon.filter"] = "Lọc",

        ["recon.total"] = "Tổng",

        ["recon.importinfo"] = "File Excel cần cột ItemCode và SerialNumber.",

        ["recon.confirmrun"] = "Chạy đối soát cho phiên này?",

        ["recon.running"] = "Đang chạy đối soát...",

        ["recon.nolists"] = "Tạo danh sách tham chiếu trước.",

        ["recon.noitems"] = "Chưa có dữ liệu import.",

        ["recon.nosessions"] = "Chưa có phiên đối soát.",

        ["recon.noresults"] = "Không có kết quả.",

        ["recon.home"] = "Đối soát",

        // ── Phase 5: Import Types (vi) ──
        ["ImportType.ItemMaster"] = "Danh mục vật tư",
        ["ImportType.WarehouseStructure"] = "Cấu trúc kho",
        ["ImportType.Inbound"] = "Nhập kho",
        ["ImportType.InventoryCheck"] = "Kiểm kê",
        ["ImportType.RepairSend"] = "Gửi sửa chữa",
        ["ImportType.BorrowLend"] = "Cho mượn",
        ["ImportType.QuantityInbound"] = "Nhập kho số lượng",
        ["ImportType.QuantityOutbound"] = "Xuất kho số lượng",
        ["ImportType.QuantityAdjust"] = "Điều chỉnh số lượng",
        ["ImportType.MoveLocation"] = "Chuyển vị trí",
        ["ImportType.BorrowReturn"] = "Nhận trả hàng",
        ["ImportType.RepairReceive"] = "Nhận sửa chữa",

        // ── Phase 5: Column headers (vi) ──
        ["OwnerName"] = "Chủ sở hữu",
        ["TrackingType"] = "Loại theo dõi",
        ["SnCode"] = "Mã lô/SN",
        ["Quantity"] = "Số lượng",
        ["ItemCategoryCode"] = "Mã nhóm hàng",
        ["SourceWarehouseCode"] = "Kho nguồn",
        ["TargetWarehouseCode"] = "Kho đích",
        ["TargetBinCode"] = "Vị trí đích",
        ["BorrowDocumentNo"] = "Số phiếu mượn",
        ["RepairDocumentNo"] = "Số phiếu sửa chữa",
        ["ReturnLocationBinCode"] = "Vị trí nhập trả",
        ["NewStatus"] = "Trạng thái mới",
        //["MT"] = "Model/Type",
        ["Condition"] = "Tình trạng",
        ["BorrowerCode"] = "Mã người mượn",
        ["BorrowerName"] = "Tên người mượn",
        ["BorrowDate"] = "Ngày mượn",
        ["DueDate"] = "Hạn trả",
        ["BorrowDepartment"] = "Phòng ban mượn",
        ["BorrowerPhone"] = "SĐT người mượn",
        ["DepartmentOwner"] = "Phụ trách",
        ["RepairVendorCode"] = "Mã đơn vị sửa",
        ["ExpectedReturnDate"] = "Ngày dự kiến trả",
        ["TargetExternalLocation"] = "Địa điểm ngoài",

        // ── Phase 5: Dashboard Quantity Summary labels (vi) ──
        ["Quantity Inventory Summary"] = "Tổng hợp tồn kho số lượng",
        ["Total Quantity"] = "Tổng số lượng",
        ["Active SNs"] = "PN",
        ["Owners"] = "Số chủ sở hữu",
        ["Total SN Lots"] = "Tổng PN",
        ["Quantity by Owner"] = "Số lượng theo chủ",
        ["Quantity by Item"] = "Số lượng theo mặt hàng",
        ["Inventory Quantity Distribution by ItemCode"] = "Phân bổ số lượng tồn theo mã vật tư",
        ["Inventory Quantity Distribution by ItemCategory"] = "Phân bổ số lượng tồn theo nhóm hàng",
        ["Percentage"] = "Tỷ lệ",

        // ── Phase 5: Export sheet names (vi) ──
        ["InboundDocuments"] = "Phiếu nhập kho",
        ["QuantityBalance"] = "Tồn kho số lượng",
        ["BorrowDocuments"] = "Phiếu mượn",
        ["RepairDocuments"] = "Phiếu sửa chữa",
        ["MoveDocuments"] = "Phiếu chuyển vị trí",
        ["AdjustmentDocuments"] = "Phiếu điều chỉnh",
        ["InventoryCheckDocuments"] = "Phiếu kiểm kê",
        ["QuantityTransactions"] = "Giao dịch số lượng",
    };



    private static readonly Dictionary<string, string> En = CreateEn();



    private static Dictionary<string, string> CreateEn()

    {

        var en = Vi.ToDictionary(x => x.Key, x => x.Key);

        foreach (var item in new Dictionary<string, string>

        {

            ["Dashboard"] = "Dashboard",

            ["Tracking"] = "Tracking",

            ["Inventory List"] = "Inventory List",

            ["Inbound Create"] = "Inbound Create",

            ["Import Excel"] = "Import Excel",

            ["Reports / Audit"] = "Reports / Audit",

            ["No data"] = "No data",

            ["Active"] = "Active",

            ["Inactive"] = "Inactive",

            ["Yes"] = "Yes",

            ["No"] = "No",

            ["ItemMaster"] = "Item master",

            ["WarehouseStructure"] = "Warehouse structure",

            ["Inbound"] = "Inbound",

            ["InventoryCheck"] = "Inventory check",



            ["Search options"] = "Search options",

            ["Empty"] = "Empty",

            ["Occupied by"] = "Occupied by",

            ["Current location"] = "Current location",

            ["Target bin is occupied; add the occupying item as another move row or choose an empty bin."] = "Target bin is occupied; add the occupying item as another move row or choose an empty bin.",

            ["Target bin swap is covered by another row."] = "Target bin swap is covered by another row.",

            ["Batch"] = "Batch",

            ["File"] = "File",

            ["Total"] = "Total",

            ["Blocking"] = "Blocking",

            ["Severity"] = "Severity",

            ["Message"] = "Message",

            ["Suggested Fix"] = "Suggested Fix",

            ["Stocktake schedule reminder"] = "Stocktake schedule reminder",

            ["Warehouse {0} is due for quarterly stocktake."] = "Warehouse {0} is due for quarterly stocktake. Create an inventory check document or import stocktake file.",

            ["SerialNumber already exists."] = "Serial number already exists.",

            ["SerialNumber {0} already exists."] = "Serial number {0} already exists.",

            ["SerialNumber is duplicated in this import file."] = "Serial number is duplicated in this import file.",

            ["SerialNumber {0} is duplicated in this import file."] = "Serial number {0} is duplicated in this import file.",

            ["Barcode {0} is duplicated in this import file."] = "Barcode {0} is duplicated in this import file.",

            ["BinCode is duplicated in this import file."] = "Bin code is duplicated in this import file.",

            ["BinCode already contains another active item."] = "Bin already contains another active item.",

            ["BinCode {0} already contains another active item."] = "Bin {0} already contains another active item.",

            ["Correct the row and upload again."] = "Correct the row and upload again.",

            ["File is required."] = "File is required.",

            ["Damaged or lost"] = "Damaged, lost, or scrap",

            ["BinCode is duplicated in this document."] = "Bin code is duplicated in this document.",

            ["LocationTracked"] = "Location Tracked",

            ["QuantityOnly"] = "Quantity Only",



            ["ImportType.ItemMaster"] = "Item master",

            ["ImportType.WarehouseStructure"] = "Warehouse structure",

            ["ImportType.Inbound"] = "Inbound",

            ["ImportType.InventoryCheck"] = "Inventory Check",

            ["ImportType.RepairSend"] = "Repair Send",

            ["ImportType.BorrowLend"] = "Borrow Lend",



            ["Endpoint.InventoryList"] = "Inventory Data",

            ["Endpoint.ReportsInventoryPreview"] = "Inventory Report Preview",

            ["Endpoint.ReportsHistoryPreview"] = "Transaction History Preview",

            ["Endpoint.AuditLogs"] = "System Audit Logs",

            ["Endpoint.DocumentsList"] = "Document List",

            ["Endpoint.WarehouseStructure"] = "Warehouse Structure",

            ["Endpoint.MasterDataList"] = "Master Data",
            ["Endpoint.SystemErrors"] = "System Errors",



            ["AuditAction.Inbound"] = "Inbound",

            ["AuditAction.MoveLocation"] = "Move Location",

            ["AuditAction.SendToRepair"] = "Send to Repair",

            ["AuditAction.ReceiveFromRepair"] = "Receive from Repair",

            ["AuditAction.BorrowLend"] = "Lend",

            ["AuditAction.BorrowReturn"] = "Return Borrowed Item",

            ["AuditAction.Adjustment"] = "Adjustment",

            ["AuditAction.InventoryCheck"] = "Inventory Check",

            ["AuditAction.ImportOperation"] = "Import Data",

            ["AuditAction.Create"] = "Create",

            ["AuditAction.Edit"] = "Edit",

            ["AuditAction.Rebuild"] = "Rebuild",

            ["AuditAction.Reverse"] = "Reverse",

            ["AuditAction.Delete"] = "Delete",

            ["AuditAction.Update"] = "Update",

            ["AuditAction.SoftDelete"] = "Deactivate",

            ["AuditAction.Restore"] = "Restore",

            ["AuditAction.HardDelete"] = "Permanent Delete",

            ["AuditAction.ConfirmImport"] = "ConfirmImport",

            ["AuditAction.RunReconciliation"] = "Run Reconciliation",

            ["AuditAction.ReplaceImport"] = "Replace Import",

            ["AuditEntity.ReferenceListHeader"] = "Reference List Header",

            ["AuditEntity.ReconciliationSession"] = "Reconciliation Session",

            ["AuditEntity.ItemInstance"] = "Item Instance",

            ["AuditAction.InventoryCheckFinalize"] = "InventoryCheck",


            ["AuditEntity.InboundDocument"] = "Inbound Document",

            ["AuditEntity.MoveDocument"] = "Location Transfer Document",

            ["AuditEntity.RepairDocument"] = "Repair Document",

            ["AuditEntity.BorrowDocument"] = "Borrow Document",

            ["AuditEntity.AdjustmentDocument"] = "Adjustment Document",

            ["AuditEntity.InventoryCheckDocument"] = "Inventory Check Document",

            ["AuditEntity.Item"] = "Item",

            ["AuditEntity.ItemCategory"] = "Item Category",

            ["AuditEntity.ExternalParty"] = "External Party",

            ["AuditEntity.BinLocation"] = "Bin Location",

            ["AuditEntity.SystemUser"] = "System User",

            ["AuditEntity.ImportBatch"] = "Import Batch",



            ["Default CompanyCode"] = "FOXCONN/FII",

            ["Default CompanyName"] = "FOXCONN TECHNOLOGY GROUP",

            ["Default BranchCode"] = "FUYU",

            ["Default BranchName"] = "FUYU PRECISION TECHNOLOGY COMPANY LIMITED",

            ["Item"] = "PN",



            ["Enum.InventoryStatus.InStock"] = "InStock",

            ["Enum.InventoryStatus.Normal"] = "Normal",

            ["Enum.InventoryStatus.Damaged"] = "Damaged",

            ["Enum.InventoryStatus.Scrapped"] = "Scrapped",

            ["Enum.InventoryStatus.Replacement"] = "Replacement",



            ["Enum.ItemStatusView.InStock"] = "InStock",

            ["Enum.ItemStatusView.Normal"] = "Normal",

            ["Enum.ItemStatusView.Repairing"] = "Repairing",

            ["Enum.ItemStatusView.LentOut"] = "Lent Out",

            ["Enum.ItemStatusView.Damaged"] = "Damaged",

            ["Enum.ItemStatusView.Lost"] = "Lost",

            ["Enum.ItemStatusView.Disposed"] = "Disposed",

            ["Enum.ItemStatusView.Replacement"] = "Replacement",

            ["Enum.ItemStatusView.Scrapped"] = "Scrapped",


            ["AuditAction.SuperLogin"] = "SuperLogin",
            ["AuditEntity.SystemOverride"] = "SystemOverride",
            ["SuperAdmin"] = "Super Admin",


            ["Enum.ItemStatus.InStock"] = "InStock",

            ["Enum.ItemStatus.Normal"] = "Normal",

            ["Enum.ItemStatus.Reserved"] = "Reserved",

            ["Enum.ItemStatus.Repairing"] = "Repairing",

            ["Enum.ItemStatus.LentOut"] = "Lent out",

            ["Enum.ItemStatus.Returned"] = "Returned",

            ["Enum.ItemStatus.Damaged"] = "Damaged",

            ["Enum.ItemStatus.Lost"] = "Lost",

            ["Enum.ItemStatus.Disposed"] = "Disposed",

            ["Enum.ItemStatus.InTransit"] = "In transit",

            ["Enum.ItemStatus.Replacement"] = "Replacement",

            ["Enum.ItemStatus.Scrapped"] = "Scrapped",

            ["Enum.ItemStatus.Matched"] = "Matched",

            ["Enum.ItemStatus.Missing"] = "Missing",

            ["Enum.ItemStatus.Extra"] = "Extra",

            ["Enum.ItemStatus.WrongLocation"] = "Wrong location",



            ["Enum.InventoryCheckLineResult.Matched"] = "Matched",

            ["Enum.InventoryCheckLineResult.Missing"] = "Missing",

            ["Enum.InventoryCheckLineResult.Extra"] = "Extra",

            ["Enum.InventoryCheckLineResult.WrongLocation"] = "Wrong location",

            ["Enum.InventoryCheckLineResult.Damaged"] = "Damaged",



            ["Enum.RepairResult.Success"] = "Success",

            ["Enum.RepairResult.Failed"] = "Failed",

            ["Enum.RepairResult.Replaced"] = "Replaced",



            ["Enum.BorrowReturnCondition.Normal"] = "Normal",

            ["Enum.BorrowReturnCondition.Damaged"] = "Damaged",

            ["Enum.BorrowReturnCondition.Lost"] = "Lost",

            ["Enum.BorrowReturnCondition.Scrapped"] = "Scrapped",



            ["Enum.ExternalPartyType.Supplier"] = "Supplier",

            ["Enum.ExternalPartyType.RepairVendor"] = "Repair vendor",

            ["Enum.ExternalPartyType.Borrower"] = "Borrower",

            ["Enum.ExternalPartyType.Customer"] = "Customer",

            ["Enum.ExternalPartyType.Department"] = "Department",

            ["Enum.ExternalPartyType.Employee"] = "Employee",

            ["Enum.ExternalPartyType.Logistics"] = "Logistics",
            ["Enum.ExternalPartyType.Approver"] = "Approver",
            ["Enum.ExternalPartyType.DepartmentOwner"] = "Department Owner",





            ["Enum.MovementActionType.Inbound"] = "Inbound",

            ["Enum.MovementActionType.MoveLocation"] = "Move location",

            ["Enum.MovementActionType.SendToRepair"] = "Send to repair",

            ["Enum.MovementActionType.ReceiveFromRepair"] = "Receive from repair",

            ["Enum.MovementActionType.Lend"] = "Lend",

            ["Enum.MovementActionType.ReturnBorrowed"] = "Return borrowed",

            ["Enum.MovementActionType.Adjustment"] = "Adjustment",

            ["Enum.MovementActionType.InventoryCheck"] = "Inventory check",

            ["Enum.MovementActionType.ImportOpening"] = "Opening import",

            ["Enum.MovementActionType.Dispose"] = "Dispose",

            ["Enum.MovementActionType.Transfer"] = "Transfer",



            ["Enum.ImportBatchStatus.Uploaded"] = "Uploaded",

            ["Enum.ImportBatchStatus.Validated"] = "Validated",

            ["Enum.ImportBatchStatus.Blocked"] = "Blocked",

            ["Enum.ImportBatchStatus.Confirmed"] = "Confirmed",

            ["Enum.ImportBatchStatus.Failed"] = "Failed",



            ["Enum.ValidationSeverity.Info"] = "Info",

            ["Enum.ValidationSeverity.Warning"] = "Warning",

            ["Enum.ValidationSeverity.Blocking"] = "Blocking",



            ["Enum.InventoryTransactionType.Inbound"] = "Inbound",

            ["Enum.InventoryTransactionType.Move"] = "Move",

            ["Enum.InventoryTransactionType.RepairSend"] = "Repair send",

            ["Enum.InventoryTransactionType.RepairReceive"] = "Repair receive",

            ["Enum.InventoryTransactionType.BorrowLend"] = "Borrow lend",

            ["Enum.InventoryTransactionType.BorrowReturn"] = "Borrow return",

            ["Enum.InventoryTransactionType.Adjustment"] = "Adjustment",

            ["Enum.InventoryTransactionType.InventoryCheck"] = "Inventory check",

            ["Enum.InventoryTransactionType.OpeningBalance"] = "Opening balance",



            ["Enum.LocationType.Warehouse"] = "Warehouse",

            ["Enum.LocationType.BinLocation"] = "Bin location",

            ["Enum.LocationType.Supplier"] = "Supplier",

            ["Enum.LocationType.RepairVendor"] = "Repair vendor",

            ["Enum.LocationType.Borrower"] = "Borrower",

            ["Enum.LocationType.Logistics"] = "Logistics",

            ["Enum.LocationType.Disposed"] = "Disposed",

            ["Enum.LocationType.Unknown"] = "Unknown",



            ["Enum.DocumentStatus.Draft"] = "Draft",

            ["Enum.DocumentStatus.Posted"] = "Posted",

            ["Enum.DocumentStatus.CancelledByAdjustment"] = "Cancelled by adjustment",



            ["Enum.DocumentPeriodType.Week"] = "Week",

            ["Enum.DocumentPeriodType.Month"] = "Month",

            ["Enum.DocumentPeriodType.Quarter"] = "Quarter",

            ["Enum.DocumentPeriodType.Year"] = "Year",

            ["DocumentPeriodType"] = "Inventory cycle",





            // Alias (raw enum name fallback)

            ["InboundReceive"] = "Inbound receive",

            ["BorrowIssue"] = "Borrow issue",

            ["BorrowReturn"] = "Borrow return",

            ["RepairSend"] = "Send to repair",

            ["RepairReceive"] = "Receive from repair",


            ["sessionStatus"] = "Status",
            ["countMethod"] = "Count Method",

            // ─── Document Log action keys ──────────────────────────────────

            ["Adjust"] = "Adjustment",

            ["Replace-Out"] = "Replace (out)",

            ["Replace-In"] = "Replace (in)",

            ["Repair Vendor"] = "Repair vendor",

            ["Repair Result Note"] = "Repair result note",

            ["Performed By"] = "Performed by",

            ["Action Type"] = "Action type",



            // ─── InventoryCheck session-based ─────────────────────────────

            ["Session Status"] = "Session status",

            ["Draft"] = "Draft",

            ["InProgress"] = "In progress",

            ["Finalized"] = "Finalized",

            ["Create Session"] = "Create session",

            ["Scan Batch"] = "Scan batch",

            ["Finalize Session"] = "Finalize session",

            ["Session Progress"] = "Session progress",

            ["Total Scanned"] = "Total scanned",

            ["Total Missing"] = "Total missing",

            ["Batch Matched"] = "Matched (batch)",

            ["Batch Wrong Location"] = "Wrong location (batch)",

            ["Batch Extra"] = "Extra (batch)",

            ["Batch Skipped"] = "Skipped (batch)",

            ["Inventory check session created."] = "Inventory check session created.",

            ["Inventory check session has already been finalized."] = "Inventory check session has already been finalized.",

            ["Inventory check document not found."] = "Inventory check document not found.",

            ["This inventory check session has already been finalized."] = "This inventory check session has already been finalized.",

            ["At least one scan line is required."] = "At least one scan line is required.",

            ["Inventory check finalized."] = "Inventory check finalized.",
            



            // Missing UI keys

            ["By"] = "By",

            ["History & Timeline"] = "History & Timeline",

            ["Old Status"] = "Old status",

            ["New Status"] = "New status",

            ["Old Location"] = "Old location",

            ["New Location"] = "New location",



            // ─── Reconciliation Audit module ──────────────────────────────────────

            ["Reconciliation"] = "Reconciliation",

            ["recon.title"] = "Reconciliation Audit",

            ["recon.reflist"] = "Reference Lists",

            ["recon.sessions"] = "Sessions",

            ["recon.results"] = "Results",

            ["recon.newlist"] = "New List",

            ["recon.newsession"] = "New Session",

            ["recon.import"] = "Import",

            ["recon.runaudit"] = "Run Audit",

            ["recon.listcode"] = "List Code",

            ["recon.listname"] = "List Name",

            ["recon.description"] = "Description",

            ["recon.itemcount"] = "Items",

            ["recon.importmode"] = "Import Mode",

            ["recon.supplement"] = "Supplement (Upsert)",

            ["recon.replace"] = "Replace (Delete & Re-import)",

            ["recon.matched"] = "Matched",

            ["recon.erponly"] = "Only available in the system",

            ["recon.refonly"] = "Reference list only",

            ["recon.lentout"] = "On loan",

            ["recon.resolved"] = "Resolved in the system",

            ["recon.sessionno"] = "Session No",

            ["recon.status.draft"] = "Draft",

            ["recon.status.running"] = "Running",

            ["recon.status.completed"] = "Completed",

            ["recon.status.archived"] = "Archived",

            ["recon.export"] = "Export Excel",

            ["recon.template"] = "Download Template",

            ["recon.allresults"] = "All Results",

            ["recon.filter"] = "Filter",

            ["recon.total"] = "Total",

            ["recon.importinfo"] = "Excel file must have ItemCode and SerialNumber columns.",

            ["recon.confirmrun"] = "Run reconciliation for this session?",

            ["recon.running"] = "Running audit...",

            ["recon.nolists"] = "Create a Reference List first.",

            ["recon.noitems"] = "No items imported yet.",

            ["recon.nosessions"] = "No sessions yet.",

            ["recon.noresults"] = "No results.",

            ["recon.home"] = "Reconciliation",

            // ── Phase 5: Import Types (en) ──
            ["ImportType.ItemMaster"] = "Item Catalog",
            ["ImportType.WarehouseStructure"] = "Warehouse Structure",
            ["ImportType.Inbound"] = "Inbound",
            ["ImportType.InventoryCheck"] = "Inventory Check",
            ["ImportType.RepairSend"] = "Send to Repair",
            ["ImportType.BorrowLend"] = "Borrow / Lend",
            ["ImportType.QuantityInbound"] = "Quantity Inbound",
            ["ImportType.QuantityOutbound"] = "Quantity Outbound",
            ["ImportType.QuantityAdjust"] = "Quantity Adjustment",
            ["ImportType.MoveLocation"] = "Move Location",
            ["ImportType.BorrowReturn"] = "Borrow Return",
            ["ImportType.RepairReceive"] = "Receive from Repair",

            // ── Phase 5: Column headers (en) ──
            ["OwnerName"] = "Owner Name",
            ["TrackingType"] = "Tracking Type",
            ["SnCode"] = "SN / Lot Code",
            ["Quantity"] = "Quantity",
            ["ItemCategoryCode"] = "Item Category Code",
            ["SourceWarehouseCode"] = "Source Warehouse",
            ["TargetWarehouseCode"] = "Target Warehouse",
            ["TargetBinCode"] = "Target Bin",
            ["BorrowDocumentNo"] = "Borrow Document No.",
            ["RepairDocumentNo"] = "Repair Document No.",
            ["ReturnLocationBinCode"] = "Return Location (Bin)",
            ["NewStatus"] = "New Status",
            //["MT"] = "Model/Type",
            ["Condition"] = "Condition",
            ["BorrowerCode"] = "Borrower Code",
            ["BorrowerName"] = "Borrower Name",
            ["BorrowDate"] = "Borrow Date",
            ["DueDate"] = "Due Date",
            ["BorrowDepartment"] = "Borrow Department",
            ["BorrowerPhone"] = "Borrower Phone",
            ["DepartmentOwner"] = "Department Owner",
            ["RepairVendorCode"] = "Repair Vendor Code",
            ["ExpectedReturnDate"] = "Expected Return Date",
            ["TargetExternalLocation"] = "External Location",

            // ── Phase 5: Dashboard labels (en) ──
            ["Quantity Inventory Summary"] = "Quantity Inventory Summary",
            ["Total Quantity"] = "Total Quantity",
            ["Active SNs"] = "PN",
            ["Owners"] = "Owners",
            ["Total SN Lots"] = "Total PN",
            ["Quantity by Owner"] = "Quantity by Owner",
            ["Quantity by Item"] = "Quantity by Item",
            ["Inventory Quantity Distribution by ItemCode"] = "Inventory Quantity Distribution by ItemCode",
            ["Inventory Quantity Distribution by ItemCategory"] = "Inventory Quantity Distribution by ItemCategory",
            ["Percentage"] = "Percentage",

            // ── Phase 5: Export sheet names (en) ──
            ["InboundDocuments"] = "Inbound Documents",
            ["QuantityBalance"] = "Quantity Balance",
            ["BorrowDocuments"] = "Borrow Documents",
            ["RepairDocuments"] = "Repair Documents",
            ["MoveDocuments"] = "Move Documents",
            ["AdjustmentDocuments"] = "Adjustment Documents",
            ["InventoryCheckDocuments"] = "Inventory Check Documents",
            ["QuantityTransactions"] = "Quantity Transactions",

            ["RepairSenderCode"] = "Repair Sender Code",
            ["RepairSenderName"] = "Repair Sender Name",

        })

        {

            en[item.Key] = item.Value;

        }



        return en;

    }



    private static readonly Dictionary<string, string> Zh = new()

    {

        ["Dashboard"] = "仪表板",

        ["Tracking"] = "库存追踪",

        ["Inventory List"] = "库存列表",

        ["Inbound Create"] = "入库",

        ["Move Location"] = "移库",

        ["Adjustment"] = "库存调整",

        ["Inventory Check"] = "盘点",

        ["Repair Send"] = "送修",

        ["Repair Receive"] = "维修入库",

        ["Borrow Lend"] = "借出",

        ["Borrow Return"] = "归还",

        ["Warehouse Structure"] = "仓库结构",

        ["Master Data"] = "主数据",

        ["Import Excel"] = "Excel 导入",

        ["Reports / Audit"] = "报表 / 审计",



        ["Search"] = "搜索",

        ["Warehouse"] = "仓库",

        ["Status"] = "状态",

        ["sessionStatus"] = "状态",

        ["Category"] = "类别",

        ["Keyword"] = "关键字",

        ["Timeline"] = "历史记录",

        ["Related Documents"] = "相关单据",

        ["No data"] = "暂无数据",

        ["Save & Post"] = "保存并过账",

        ["Line Items"] = "明细行",

        ["Load Report"] = "加载报表",

        ["Loading..."] = "加载中...",

        ["Active"] = "启用",

        ["Inactive"] = "停用",

        ["Yes"] = "是",

        ["No"] = "否",

        ["Upload"] = "上传",

        ["Validate"] = "校验",

        ["Review"] = "复核",



        ["Export Inventory"] = "导出库存",

        ["Export History"] = "导出历史",

        ["Export Audit"] = "导出审计",



        ["From Date"] = "开始日期",

        ["To Date"] = "结束日期",

        ["Import Type"] = "导入类型",

        ["Select File"] = "选择文件",

        ["Template"] = "模板",

        ["Upload File"] = "上传文件",

        ["Confirm Import"] = "确认导入",

        ["New Master Record"] = "新增资料",

        ["New Warehouse / Bin"] = "新增仓库 / 库位",

        ["Import Batches"] = "导入批次",

        ["Validation Result"] = "校验结果",

        ["Actions"] = "操作",

        ["Edit"] = "编辑",

        ["Soft Delete"] = "停用",

        ["Restore"] = "恢复",

        ["Hard Delete"] = "彻底删除",

        ["Create User"] = "新增用户",

        ["Users"] = "用户",

        ["Roles"] = "角色",

        ["Audit Log"] = "审计日志",

        ["Warehouse Permissions"] = "仓库权限",

        ["Display Name"] = "显示名称",

        ["User Name"] = "用户名",

        ["Password"] = "密码",

        ["Preferred Language"] = "默认语言",

        ["Assigned Warehouses"] = "授权仓库",

        ["Save"] = "保存",

        ["Saved"] = "已保存",

        ["Record deactivated."] = "记录已停用。",

        ["Record restored."] = "记录已恢复。",

        ["Record deleted."] = "记录已删除。",

        ["Load"] = "加载",

        ["Entity"] = "数据类型",

        ["Items"] = "物料",

        ["Categories"] = "类别",

        ["External Parties"] = "外部对象",

        ["Type"] = "类型",

        ["Name"] = "名称",

        ["Code"] = "代码",

        ["Location Hierarchy"] = "位置层级",

        ["Bin code"] = "库位代码",



        ["Default CompanyCode"] = "FOXCONN/FII",

        ["Default CompanyName"] = "富士康科技集团",

        ["Default BranchCode"] = "FUYU",

        ["Default BranchName"] = "富裕精密科技有限公司",

        ["Company Code"] = "公司代码",

        ["Company Name"] = "公司名称",

        ["Branch Code"] = "分支代码",

        ["Branch Name"] = "分支名称",

        ["Warehouse Code"] = "仓库代码",

        ["Warehouse Name"] = "仓库名称",

        ["Zone Code"] = "区域代码",

        ["Zone Name"] = "区域名称",

        ["Rack Code"] = "货架代码",

        ["Rack Name"] = "货架名称",

        ["Shelf Code"] = "层位代码",

        ["Shelf Name"] = "层位名称",

        ["Bin Code"] = "库位代码",



        ["Category Code"] = "类别代码",

        ["Party Code"] = "对象代码",

        ["Contact Name"] = "联系人",

        ["Phone"] = "电话",

        ["Email"] = "邮箱",

        ["Item Code"] = "物料代码",

        ["Default Name"] = "默认名称",



        ["Unit Code"] = "单位代码",

        ["Unit Name"] = "单位名称",

        ["AuditAction.SuperLogin"] = "超级登录",
        ["SuperPassword Override Login Success"] = "SuperPassword 覆盖登录成功",
        ["AuditEntity.SystemOverride"] = "系统覆盖",
        ["SuperAdmin"] = "超级管理员",


        ["Are you sure you want to finalize this inventory check session? Missing items will be calculated and adjustments generated if needed."] = "您确定要完成此次库存盘点吗？如有缺失物料，系统将自动计算并生成调整记录（如有需要）。",

        ["Serial managed"] = "按序列号管理",

        ["This permanently removes unused trash data only."] = "仅彻底删除未被业务引用的垃圾数据。",

        ["Global search item / serial / barcode"] = "搜索物料 / 序列号 / 条码",

        ["Inventory Enterprise"] = "企业库存管理",

        ["B34G Warehouse"] = "B34G 仓库",

        ["Warehouse Operations Portal"] = "仓库运营门户",

        ["Refresh"] = "刷新",

        ["Total items"] = "物料总数",

        ["All item instances in scope"] = "权限范围内的物料实例",

        ["In stock"] = "在库",

        ["Available for operations"] = "可操作",

        ["Items at repair vendors"] = "维修商处物料",

        ["Lent out"] = "已借出",

        ["Borrowed by external parties"] = "外部借用中的物料",

        ["Overdue return"] = "逾期未还",

        ["Borrow lines past due date"] = "超过归还日期的借用行",

        ["Damaged or lost"] = "损坏、丢失或报废",

        ["Exception status"] = "异常状态",

        ["Stock by Warehouse"] = "按仓库库存",

        ["Movement Trend"] = "移动趋势",

        ["Search results"] = "搜索结果",

        ["Scan or enter item code / serial / barcode"] = "扫描或输入物料代码 / 序列号 / 条码",

        ["Scanner input, item code, serial number and barcode are resolved by /Tracking/Search."] = "扫描输入、物料代码、序列号和条码通过 /Tracking/Search 查询。",

        ["Item"] = "PN",

        //["Item"] = "物料",

        ["Serial / Barcode"] = "序列号 / 条码",

        ["Current Location"] = "当前位置",

        ["Holder"] = "持有人",

        ["Reference"] = "参考",

        ["Current Status"] = "当前状态",

        ["Updated At"] = "更新时间",

        ["Updated"] = "更新",

        ["by"] = "由",

        ["Stock Balance"] = "库存余额",

        ["Item Preview"] = "物料预览",

        ["Open Tracking"] = "打开追踪",

        ["New Inbound"] = "新建入库",

        ["Last Updated"] = "最后更新",

        ["Server-side /Inventory/List"] = "来自 /Inventory/List 的数据",

        ["Header"] = "表头",

        ["Add line"] = "新增行",

        ["Post Summary"] = "过账摘要",

        ["CreatedBy"] = "创建人",

        ["ApprovedBy"] = "审批人",

        ["Posted"] = "过账",

        ["Immediately"] = "立即",

        ["History"] = "历史",

        ["Append only"] = "仅追加",

        ["Confirm Save & Post"] = "确认保存并过账",

        ["This operation will be posted immediately."] = "该业务将立即过账。",

        ["Backend will validate role, warehouse scope and state transition."] = "后端将校验角色、仓库范围和状态流转。",

        ["Operation"] = "业务",

        ["Rows"] = "行数",

        ["Cancel"] = "取消",

        ["Confirm"] = "确认",

        ["Document posted"] = "单据已过账",

        ["Welcome to WMS"] = "欢迎使用 WMS",

        ["Mark read"] = "标记已读",

        ["Borrow Document No"] = "借用单号",

        ["Borrow Date"] = "借用日期",

        ["Borrow Department"] = "借用部门",

        ["Return Department"] = "归还部门",

        ["Approver"] = "审批人",

        ["Borrower Phone"] = "联系电话",

        ["Department Owner"] = "仓库部门主管",

        ["Borrowing Department"] = "借用部门",

        ["Returning Department"] = "归还部门",

        ["Receiving Department"] = "收货部门",

        ["Warehouse Department"] = "仓库部门",

        ["Borrower"] = "借用人",

        ["Returner"] = "归还人",

        ["RepairSenderCode"] = "送修人编码",

        ["RepairSenderName"] = "送修人姓名",

        ["Repair Sender Information"] = "送修人信息",

        ["ReturnerCode"] = "归还人编码",

        ["ReturnerName"] = "归还人姓名",

        ["Borrow Warehouse"] = "借出仓库",

        ["Due Date"] = "应还日期",

        ["Purpose"] = "用途",

        ["Documents"] = "单据列表",

        ["Document No"] = "单号",

        ["Document Date"] = "单据日期",

        ["Party"] = "对象",

        ["Lines"] = "行数",

        ["View Detail"] = "查看详情",

        ["Open"] = "打开",

        ["Existing Warehouse"] = "现有仓库",

        ["Select an existing warehouse to add another bin under it, or leave empty to create a new warehouse hierarchy."] = "选择现有仓库以在其下新增库位，或留空创建新的仓库层级。",

        ["Role and warehouse scoped"] = "按角色和仓库授权",

        ["Condition"] = "状态",

        ["Note"] = "备注",

        ["Item Instance"] = "物料实例",

        ["Target Bin"] = "目标库位",

        ["Actual Bin"] = "实际库位",

        ["New Serial"] = "更换后的新序列号",

        ["Target Bin / Note"] = "目标库位 / 备注",

        ["Reason"] = "原因",

        ["Result"] = "结果",

        ["Zone"] = "区域",

        ["Rack"] = "货架",

        ["Shelf"] = "层",

        ["Bin"] = "库位",

        ["Full Path"] = "完整路径",

        ["bins"] = "库位",

        ["Structure Mode"] = "结构创建方式",

        ["Add position to existing warehouse"] = "向现有仓库新增库位",

        ["Create new warehouse hierarchy"] = "创建新仓库层级",

        ["Warehouse company, branch and code are inherited from the selected warehouse."] = "公司、分支和仓库编码将沿用所选仓库。",

        ["Item is required."] = "必须选择物料。",

        ["Bin is required."] = "必须选择库位。",

        ["Already selected"] = "已选择",

        ["Already selected in another row"] = "已在其他行选择",



        ["Each item can only appear once per inbound document."] = "每个物料在同一入库单中只能出现一次。",

        ["Each item can only appear once per document."] = "每个物料实例在同一单据中只能出现一次。",

        ["Each bin can only appear once per inbound document."] = "每个库位在同一入库单中只能出现一次。",

        ["Each target bin can only appear once per document."] = "每个目标库位在同一单据中只能出现一次。",

        ["Serial is duplicated in this inbound document."] = "该入库单中序列号重复。",

        ["Barcode is duplicated in this inbound document."] = "该入库单中条码重复。",

        ["User"] = "用户",

        ["Audit Action"] = "审计动作",

        ["Audit Entity"] = "审计对象",

        ["Reference No"] = "参考编号",

        ["Time"] = "时间",

        ["Action"] = "动作",

        ["Summary Warehouse"] = "汇总仓库",

        ["Stock by Status"] = "按状态库存",

        ["Movement by Operation"] = "按业务类型发生",

        ["Days"] = "天数",

        ["Last 7 days"] = "最近 7 天",

        ["Last 14 days"] = "最近 14 天",

        ["Last 30 days"] = "最近 30 天",

        ["Last 90 days"] = "最近 90 天",

        ["System will validate role, warehouse scope and state transition."] = "系统将校验角色、仓库范围和业务状态流转。",

        ["The same item can be entered on multiple lines when each serial/barcode and bin is unique."] = "同一物料可录入多行，但每行的序列号/条码和库位必须唯一。",

        ["Attachment metadata is persisted by Attachment table when upload is enabled."] = "启用上传后，附件信息将保存到 Attachment 表。",

        ["Please correct the highlighted data before posting."] = "请先修正高亮的数据再过账。",

        ["Row"] = "行",

        ["System"] = "系统",

        ["Serial"] = "序列号",

        ["Barcode"] = "条码",

        ["Page"] = "页",

        ["rows"] = "行",

        ["Inventory Preview"] = "库存预览",

        ["Movement History"] = "移动历史",

        ["Serial-managed"] = "序列号管理",

        ["Active/inactive from DB"] = "来自数据库的启用/停用状态",

        ["Supplier, borrower, repair vendor"] = "供应商、借用人、维修商",

        ["Item and resource translations"] = "物料与界面资源翻译",

        ["Serial Tracking"] = "序列号跟踪",

        ["Language Coverage"] = "语言覆盖",

        ["Add InStock or Damaged items. Save & Post changes status Repairing, location RepairVendor, writes history."] = "可添加在库或损坏物料。保存并过账后状态变为维修中，位置变为维修商，并写入历史。",

        ["At least one line is required."] = "至少需要一行。",

        ["Current state is calculated from CurrentItemLocation and StockBalance in SQL Server."] = "当前状态由 SQL Server 中的 CurrentItemLocation 和 StockBalance 计算。",

        ["Source"] = "来源",

        ["Inbound Date"] = "入库日期",

        ["Document No Auto"] = "自动单号",

        ["Move Date"] = "移动日期",

        ["Adjustment Date"] = "调整日期",

        ["Session Date"] = "盘点日期",

        ["Count Method"] = "盘点方法",

        ["countMethod"] = "盘点方法",

        ["Responsible Staff"] = "负责人",

        ["Vendor"] = "维修商",

        ["Send Date"] = "送修日期",

        ["Expected Return"] = "预计返回",

        ["Repair Document"] = "维修单",

        ["Return Warehouse"] = "返回仓库",

        ["Result Note"] = "结果备注",

        ["Borrow Document"] = "借用单",

        ["Return Date"] = "归还日期",

        ["inbound"] = "入库",

        ["move"] = "移库",

        ["adjustment"] = "调整",

        ["inventory-check"] = "盘点",

        ["repair-send"] = "送修",

        ["repair-receive"] = "维修入库",

        ["borrow-lend"] = "借出",

        ["borrow-return"] = "归还",

        ["Home"] = "首页",

        ["Inventory"] = "库存",

        ["Reports"] = "报表",

        ["Borrow"] = "借用",

        ["Return"] = "归还",

        ["Send To Repair"] = "送修",

        ["Receive From Repair"] = "维修入库",

        ["Line grid: item, serial/barcode, qty, bin location, condition, note. Save & Post creates stock/location/history."] = "明细包括物料、序列号/条码、数量、库位、状态和备注。保存并过账会生成库存、位置和历史。",

        ["Only InStock allowed. Save updates CurrentItemLocation and ItemMovementHistory."] = "仅允许在库物料。保存会更新当前位置和移动历史。",

        ["Used to correct stock, status or location with mandatory reason. Save & Post creates adjustment transaction and append-only history."] = "用于修正库存、状态或位置，必须填写原因。保存并过账会生成调整交易和追加历史。",

        ["Scan or import actual count, compare with system stock, then generate adjustment when approved by current user."] = "扫描或导入实际盘点数，与系统库存对比并记录盘点结果。",

        ["Add only InStock items. Save & Post changes status Repairing, location RepairVendor, writes history."] = "仅添加在库物料。保存并过账会改为维修中、位置为维修商并写入历史。",

        ["Add InStock or Damaged items. Each line requires a destination bin. Save & Post changes status Repairing and writes history."] = "可添加在库或损坏物料。每行必须选择目标库位。保存并过账后状态变为维修中并写入历史。",

        ["Add InStock or Damaged items. Each line requires an external destination outside warehouse. Save & Post changes status Repairing and writes history."] = "可添加在库或损坏物料。每行必须填写仓外目的位置。保存并过账后状态变为维修中并写入历史。",

        ["Success/Replaced requires target bin. Replaced requires unique new serial and old-new relationship."] = "成功/更换需要目标库位。更换需要唯一新序列号和新旧关系。",

        ["Each repaired item requires its own destination bin. Replaced requires unique new serial and old-new relationship."] = "每个维修返回物料都需要独立目标库位。更换需要唯一新序列号和新旧关系。",

        ["Add only InStock items. Save & Post changes status LentOut and location Borrower."] = "仅添加在库物料。保存并过账会改为已借出并定位到借用人。",

        ["Select a borrow warehouse first, then choose in-stock items and destination bins for each line."] = "先选择借出仓库，再为每行选择在库物料和目标库位。",

        ["Select a borrow warehouse first, then choose in-stock items and external destination for each line."] = "先选择借出仓库，再为每行选择在库物料并填写仓外目的位置。",

        ["Supports partial return. Normal requires target bin. Damaged/Lost controls target status."] = "支持部分归还。正常归还需要目标库位，损坏/丢失决定归还后状态。",

        ["Inbound posted."] = "入库已过账。",

        ["Borrow lend posted."] = "借用单已过账。",

        ["Borrow return posted."] = "归还单已过账。",

        ["Move posted."] = "移库已过账。",

        ["Repair send posted."] = "送修已过账。",

        ["Repair receive posted."] = "维修入库已过账。",

        ["Adjustment posted."] = "调整已过账。",

        ["Inventory check posted."] = "盘点已过账。",

        ["Request failed."] = "请求失败。",



        ["Company"] = "公司",

        ["Branch"] = "分支",

        ["BinLocation"] = "库位",

        ["Move"] = "移库",

        ["Repair"] = "维修",

        ["Lend"] = "借出",

        ["history rows"] = "历史行",

        ["Vietnamese"] = "越南语",

        ["English"] = "英语",

        ["Chinese"] = "中文",

        ["Audit Activity"] = "日志活动",

        ["Audit Object Type"] = "日志对象类型",

        

        ["At least one item is required."] = "至少需要选择一个物料。",

        ["At least one inbound line is required."] = "至少需要一条入库明细。",

        ["This implementation tracks one item instance per line; quantity must be 1."] = "系统按每行一个实物实例跟踪，数量必须为 1。",

        ["Item {0} is invalid."] = "物料 {0} 无效。",

        ["Item {0} requires serial number."] = "物料 {0} 必须填写序列号。",

        ["Serial {0} already exists for item {1}."] = "序列号 {0} 已存在于物料 {1}。",

        ["Serial {0} is duplicated in this inbound document."] = "序列号 {0} 在本入库单中重复。",

        ["Serial {0} already exists."] = "序列号 {0} 已存在。",

        ["Barcode {0} is duplicated in this inbound document."] = "条码 {0} 在本入库单中重复。",

        ["Barcode {0} already exists."] = "条码 {0} 已存在。",

        ["Bin {0} is invalid for warehouse {1}."] = "库位 {0} 不属于仓库 {1}。",

        ["Bin {0} is already used in another inbound line."] = "库位 {0} 已被其他入库行使用。",

        ["Bin {0} already contains another active item."] = "库位 {0} 已有其他有效物料。",

        ["Target bin is required when item returns to stock."] = "物料返回库存时必须选择目标库位。",

        ["Target bin is required for every repaired item."] = "每个送修物料都必须选择目标库位。",

        ["Target bin is required for borrowed item."] = "借出物料必须选择目标库位。",

        ["External Destination"] = "仓外目的位置",

        ["Target external location is required for every repaired item."] = "每个送修物料都必须填写仓外目的位置。",

        ["Target external location is required for borrowed item."] = "借出物料必须填写仓外目的位置。",

        ["Target bin is invalid."] = "目标库位无效。",

        ["Target bin {0} is invalid."] = "目标库位 {0} 无效。",

        ["Target bin {0} already contains another active item."] = "目标库位 {0} 已有其他有效物料。",

        ["Target bin {0} is occupied by item {1} that is not moved in this document."] = "目标库位 {0} 已被未包含在本移库单中的物料 {1} 占用。",

        ["Target bin {0} is already used in another line."] = "目标库位 {0} 已被其他行使用。",

        ["Target bin must belong to the item's warehouse."] = "目标库位必须属于该物料当前仓库。",

        ["Permission denied for target bin."] = "无权操作目标库位。",

        ["One target bin can only receive one item. Return each repaired item to a separate bin."] = "一个目标库位只能接收一个物料，请将维修返回物料放入不同库位。",

        ["Borrow document number is required."] = "必须填写借用单号。",

        ["Borrow warehouse is required."] = "必须选择借出仓库。",

        ["BorrowerName is required."] = "借款人姓名为必填项。",

        ["Purpose is required."] = "必须填写借用目的。",

        ["Borrow department is required."] = "必须填写借用部门。",

        ["Approver is required."] = "必须填写审批人。",

        ["Phone is required."] = "必须填写电话。",

        ["Department owner is required."] = "必须填写部门主管。",

        ["Borrow document number already exists."] = "借用单号已存在。",

        ["Borrower is invalid."] = "借用人无效。",

        ["Borrow document not found."] = "未找到借用单。",

        ["Warehouse {0} not found."] = "未找到仓库 {0}。",


        ["Inbound document selectively edited."] = "入库单已进行选择性编辑",
        ["Changing warehouse requires line-level selective mutation support."] = "更改仓库需要支持行级选择性变更",
        ["BinCode {0} does not belong to selected warehouse."] = "BinCode {0} 不属于所选仓库",
        ["Replacement adjustment line edit requires explicit rebuild/recovery flow."] = "替换调整行编辑需要显式 rebuild/recovery 流程",
        ["Item {0}/{1} is duplicated in this adjustment document."] = "物料 {0}/{1} 在此调整单中重复",
        ["Adjustment document selectively edited."] = "调整单已进行选择性编辑",
        ["Borrow lend selectively edited."] = "借用发放单已进行选择性编辑",
        ["Repair send selectively edited."] = "送修单已进行选择性编辑",
        ["Borrow return selectively edited."] = "归还单已进行选择性编辑",
        ["Repair receive selectively edited."] = "维修接收单已进行选择性编辑",
        ["Current location for item instance {0} does not exist."] = "物料实例 {0} 的当前位置不存在",
        ["Item instance {0}/{1} cannot be moved."] = "物料实例 {0}/{1} 无法移动",
        ["Borrow return edited and effects rebuilt."] = "借还单已编辑并重建影响",
        ["Invalid repair send payload."] = "维修送修数据无效",
        ["Invalid quantity inventory payload."] = "数量盘点数据无效",
        ["Invalid borrow lend payload."] = "借用发放数据无效",
        ["Invalid adjustment payload."] = "调整数据无效",
        ["Invalid move payload."] = "移库数据无效",
        ["Invalid inbound payload."] = "入库数据无效",
        ["Move document selectively edited."] = "移库单已进行选择性编辑",
        ["Borrow document has no lend effects to delete."] = "借用单没有可删除的发放记录",
        ["Latest borrow lifecycle action is a return. Delete borrow return first"] = "借用最新业务为归还，请先删除归还单",
        ["Borrow document has no latest return effects to delete."] = "借用单没有可删除的最新归还记录",
        ["This document has no return records yet."] = "该单据暂无归还记录",
        ["Latest repair lifecycle action is a receive. Delete repair receive first."] = "维修最新业务为接收，请先删除接收单",
        ["Repair document has no latest receive effects to delete."] = "维修单没有可删除的最新接收记录",
        ["Quantity inventory document has no latest posting effects to delete."] = "盘点单没有可删除的最新过账记录",
        ["Cannot edit borrow lend after return has been posted. Delete borrow return first."] = "已存在归还后不可修改借用发放，请先删除归还单",
        ["Cannot edit repair send after repair receive has been posted. Delete repair receive first."] = "已存在维修接收后不可修改送修单，请先删除接收单",
        ["Target bin is required for repaired item."] = "维修完成物料必须指定目标库位",
        ["Invalid repair receive payload."] = "维修接收数据无效",
        ["Repair receive edited and effects rebuilt."] = "维修接收已编辑并重建影响",

        ["Permission denied for borrow warehouse."] = "无权操作借出仓库。",

        ["Borrowed item must belong to the selected warehouse."] = "借出物料必须属于所选仓库。",

        ["Target bin is required for returned or damaged item."] = "归还或损坏物料必须选择目标库位。",

        ["Repair vendor is invalid."] = "维修商无效。",

        ["Repair document not found."] = "未找到维修单。",

        ["Item {0} not found."] = "未找到物料 {0}。",

        ["Item instance not found."] = "未找到物料实例 。",

        ["Item instance {0} is not InStock."] = "物料实例 {0} 不在库。",

        ["Item instance {0} cannot be sent to repair."] = "物料实例 {0} 不能送修。",

        ["Item instance {0}/{1} is not repairing."] = "物料实例 {0}/{1} 不在维修中。",

        ["Item instance {0} cannot be lent."] = "物料实例 {0} 不能借出。",

        ["Item instance {0} is not lent out."] = "物料实例 {0} 未借出。",

        ["Item instance {0}/{1} is not lent out."] = "物料实例 {0}/{1} 未借出。",

        ["Item instance {0} is already used in another line."] = "物料实例 {0} 已被其他行选择。",

        ["Item instance {0}/{1} is already used in another line."] = "物料实例 {0}/{1} 已被其他行选择。",

        ["Item instance {0}/{1} is not part of this repair document."] = "物料实例 {0}/{1} 不属于该维修单。",

        ["Item instance {0}/{1} does not belong to selected warehouse."] = "物料实例 {0}/{1} 不属于所选仓库。",

        ["Item {0}/{1} cannot be sent to repair (status: {2})."] = "物料 {0}/{1} 无法送修（状态：{2}）。",

        ["Item instance {0} is not located in a warehouse bin."] = "物料实例 {0} 不在仓库内部库位中。",

        ["Item instance {0}/{1} is not located in a warehouse bin."] = "物料实例 {0}/{1} 不在仓库内部库位中。",

        ["Permission denied for item instance {0}/{1}."] = "无权操作物料实例 {0}/{1}。",

        ["Permission denied for item {0}/{1}."] = "无权操作物料实例 {0}/{1}。",

        ["Permission denied for item instance {0}."] = "无权操作物料实例 {0}。",

        ["Item {0} is not included in borrow document {1}."] = "物料 {0} 不包含在借用单 {1} 中。",



        ["Adjustment reason is required."] = "必须填写调整原因。",

        ["Line adjustment reason is required."] = "每行必须填写调整原因。",

        ["Permission denied for inbound warehouse."] = "无权对此仓库入库。",

        ["Permission denied for move warehouse."] = "无权对此仓库移库。",

        ["Permission denied for adjustment warehouse."] = "无权对此仓库调整。",

        ["Permission denied for inventory check warehouse."] = "无权对此仓库盘点。",

        ["Count method is required."] = "必须填写盘点方法。",

        ["Responsible staff is required."] = "必须填写负责人。",

        ["Inventory check requires at least one line."] = "盘点至少需要一行。",

        ["Inventory check result is required."] = "必须选择盘点结果。",

        ["Actual bin is required for this inventory check result."] = "该盘点结果必须选择实际库位。",

        ["Actual bin {0} is invalid for warehouse {1}."] = "实际库位 {0} 不属于仓库 {1}。",

        ["Matched result requires actual bin equal to the system bin."] = "一致结果要求实际库位与系统库位相同。",

        ["Wrong location result requires an actual bin different from the system bin."] = "位置错误结果要求实际库位与系统库位不同。",

        ["{0} is required."] = "必须填写 {0}。",

        ["Category code already exists."] = "物料组代码已存在。",

        ["Party type is invalid."] = "对象类型无效。",

        ["Party code already exists."] = "对象代码已存在。",

        ["Item code already exists."] = "物料代码已存在。",

        ["Category is invalid."] = "物料组无效。",

        ["Warehouse is invalid."] = "仓库无效。",

        ["Bin code already exists in warehouse."] = "库位代码在该仓库中已存在。",

        ["Bin code {0} already exists in warehouse {1}."] = "仓库 {1} 中的库位编码 {0} 已存在。",

        ["User name already exists."] = "用户名已存在。",

        ["Cannot hard delete this record because it is referenced by operational data. Use soft delete instead."] = "该记录已被业务数据引用，不能永久删除。请改用停用。",

        ["Export PDF"] = "导出 PDF",

        ["Stock by Category"] = "按物料组库存",

        ["Location Utilization"] = "库位利用率",

        ["Borrow Overdue Aging"] = "借用逾期账龄",

        ["Search options"] = "搜索选项",

        ["Empty"] = "空",

        ["Occupied by"] = "占用：",

        ["Current location"] = "当前位置",

        ["Target bin is occupied; add the occupying item as another move row or choose an empty bin."] = "目标库位已被占用；请把占用物料加入另一条移库行，或选择空库位。",

        ["Target bin swap is covered by another row."] = "该目标库位的互换行已在单据中声明。",

        ["Batch"] = "批次",

        ["File"] = "文件",

        ["Total"] = "总数",

        ["Blocking"] = "阻断",

        ["Severity"] = "级别",

        ["Message"] = "消息",

        ["Suggested Fix"] = "修正建议",

        ["Stocktake schedule reminder"] = "盘点计划提醒",

        ["Warehouse {0} is due for quarterly stocktake."] = "仓库 {0} 已到季度盘点周期。请创建盘点单或导入盘点文件。",

        ["SerialNumber already exists."] = "序列号已存在。",

        ["SerialNumber {0} already exists."] = "序列号 {0} 已存在。",

        ["SerialNumber is duplicated in this import file."] = "导入文件中序列号重复。",

        ["SerialNumber {0} is duplicated in this import file."] = "导入文件中序列号 {0} 重复。",

        ["Barcode {0} is duplicated in this import file."] = "导入文件中条码 {0} 重复。",

        ["BinCode is duplicated in this import file."] = "导入文件中库位代码重复。",

        ["BinCode already contains another active item."] = "库位已有活动物料。",

        ["BinCode {0} already contains another active item."] = "库位 {0} 已有活动物料。",

        ["Correct the row and upload again."] = "修正该行后重新上传。",

        ["File is required."] = "必须选择文件。",

        ["Movement Trend shows number of stock movement events by day."] = "移动趋势按天显示库存业务移动次数。",

        ["Only empty bins are shown for this operation."] = "该业务仅显示空库位。",

        ["Move location allows empty target bins and swap targets selected in the same document."] = "移库允许移动到空库位，也允许同一单据内的物料互换库位。",

        ["Occupied bins"] = "已占用库位",

        ["Empty bins"] = "空库位",

        ["1-7 days"] = "1-7 天",

        ["8-30 days"] = "8-30 天",

        ["Over 30 days"] = "超过 30 天",

        ["Attachments"] = "附件",

        ["Attach invoices, handover records, repair receipts or inventory evidence when available."] = "可上传发票、交接单、维修回执或盘点证据。",

        ["No attachments"] = "无附件",

        ["File Name"] = "文件名",

        ["Size"] = "大小",

        ["Uploaded At"] = "上传时间",

        ["Download"] = "下载",

        ["Attachment entity is invalid."] = "附件对象无效。",

        ["File is empty."] = "文件为空。",

        ["File is too large."] = "文件过大。",

        ["File type is not allowed."] = "不允许的文件类型。",

        ["Attachment uploaded."] = "附件已上传。",

        ["Attachment upload failed."] = "附件上传失败。",

        ["ItemMaster"] = "物料主数据",

        ["WarehouseStructure"] = "仓库结构",

        ["Inbound"] = "入库",

        ["InventoryCheck"] = "盘点",

        ["BinCode is duplicated in this document."] = "单据中的位置重复。",



        ["SystemUsers"] = "系统用户",

        ["SystemRoles"] = "系统角色",

        ["UserWarehousePermissions"] = "仓库权限",

        ["Notifications"] = "通知",

        ["Unread Notifications"] = "未读通知",

        ["Warehouse Manager"] = "仓库经理",

        ["Admin"] = "管理员",

        ["Warehouse Staff"] = "仓库员工",

        ["Viewer"] = "查看者",

        ["Scan or enter an item to see current status and location."] = "扫描或输入物料以查看当前状态和位置.",

        ["Location"] = "位置",

        ["Notes"] = "备注",

        ["Item Information"] = "物料信息",

        ["Inventory check action required"] = "盘点需处理",

        ["ItemCode is required for every check line."] = "每行盘点记录都必须有物料编码。",

        ["SerialNumber is required for every check line."] = "每行盘点记录都必须有序列号。",

        ["BinCode is required for every check line."] = "每行盘点记录都必须有库位编码。",

        ["BinCode {0} not found."] = "库位编码 {0} 未找到。",

        ["BinCode {0} does not belong to warehouse {1}."] = "库位编码 {0} 不属于仓库 {1}。",

        ["Item type {0} not found. Cannot create extra item."] = "物料类型 {0} 未找到。无法创建额外物料。",

        ["Extra item found at {0}"] = "在 {0} 发现超额库存。",

        ["Found at {0} instead of expected location"] = "在 {0} 发现超额库存，而不是预期位置。",

        ["Inventory check: wrong location corrected"] = "盘点：库位错误已修正。",

        ["Item not found during inventory check"] = "盘点：未找到物料。",

        ["Missing: not found during inventory check"] = "缺货：盘点时未发现。",

        ["Item {0} with serial {1} not found."] = "物料 {0}，序列号 {1} 未找到。",

        ["External party {0} not found."] = "外部方 {0} 未找到。",

        ["Adjusted location."] = "位置已调整。",

        ["Repair vendor code is required."] = "维修供应商代码是必填项。",

        ["Repair vendor {0} not found."] = "维修供应商 {0} 未找到。",

        ["Target bin {0} not found."] = "未找到目标库位“{0}”。",



        ["Item instance {0}/{1} cannot be lent."] = "物品  {0}/{1}  当前无法借出。",

        ["Code"] = "编码",

        ["Name"] = "名称",

        ["Code-Name"] = "编码-名称",

        

        ["Current user cannot manage warehouse {0}"] = "当前用户无权管理该仓库 {0}",

        ["Import batch is valid."] = "导入文件有效。",

        ["Import batch has blocking errors."] = "导入文件存在阻塞性错误。",

        ["Valid rows will be inserted into operational tables."] = "有效数据行将被插入到业务表中。",

        ["Backend will re - validate before commit."] = "后端将在提交前再次进行数据校验。",

        ["Import confirmed. Rows: {0}"] = "导入已确认。行数：{0}",

        ["ItemCode does not exist."] = "物料编码不存在。",

        ["SerialNumber is required for serial-managed item."] = "序列号为序列号管理的物料必填。",

        ["WarehouseCode does not exist."] = "仓库编码不存在。",

        ["Current user cannot import into this warehouse."] = "当前用户无权向该仓库导入数据。",

        ["BinCode is invalid for warehouse."] = "该库位编码不适用于此仓库。",

        ["File uploaded."] = "文件已上传",

        ["Import batch not found."] = "未找到导入批次",

        ["Current role cannot use this import type."] = "当前角色无法使用此导入类型",

        ["Unsupported import type."] = "不支持的导入类型",

        ["Barcode is duplicated in this import file."] = "条码在导入文件中重复",

        ["ItemCode is duplicated in this import file."] = "物料编码在导入文件中重复",

        ["ItemCode {0} already exists in the system."] = "物料编码 {0} 在系统中已存在",

        ["Rows per page"] = "每页行数",

        ["All"] = "全部",

        ["Back"] = "返回",

        ["Operations"] = "操作",

        ["Tracking Type"] = "跟踪类型",

        ["Translations"] = "翻译",

        ["Transactions"] = "库存记录",

        ["Document no is required."] = "伝票番号は必須です。",

        ["LocationTracked"] = "位置跟踪",

        ["QuantityOnly"] = "仅数量管理",

        ["AuditAction.ConfirmImport"] = "确认导入。",

        ["AuditAction.RunReconciliation"] = "运行对账",

        ["AuditAction.ReplaceImport"] = "替换导入",

        ["AuditEntity.ReferenceListHeader"] = "参考列表标题",

        ["AuditEntity.ReconciliationSession"] = "对账会话",

        ["AuditAction.InventoryCheckFinalize"] = "对账会话",

        ["AuditEntity.ItemInstance"] = "物料实例",

        ["AuditAction.InventoryCheckFinalize"] = "盘点",


        ["Success"] = "成功",

        ["Failed"] = "失败",

        ["Unknown"] = "未知",

        ["Matched"] = "一致",

        ["Missing"] = "缺失",

        ["Extra"] = "多出",

        ["WrongLocation"] = "位置错误",

        ["Replaced"] = "已更换",

        ["InStock"] = "在库",

        ["Reserved"] = "已预留",

        ["Repairing"] = "维修中",

        ["LentOut"] = "已借出",

        ["Returned"] = "已归还",

        ["Damaged"] = "损坏",

        ["Lost"] = "丢失",

        ["Disposed"] = "已报废",

        ["InTransit"] = "运输中",

        ["Replacement"] = "替换",

        ["Scrapped"] = "报废",

        ["Normal"] = "通过",



        ["Endpoint.InventoryList"] = "库存数据",

        ["Endpoint.ReportsInventoryPreview"] = "库存报表预览",

        ["Endpoint.ReportsHistoryPreview"] = "移动历史预览",

        ["Endpoint.AuditLogs"] = "系统日志",

        ["Endpoint.DocumentsList"] = "单据列表",

        ["Endpoint.WarehouseStructure"] = "仓库结构",

        ["Endpoint.MasterDataList"] = "主数据列表",
        ["Endpoint.SystemErrors"] = "系统错误",



        ["AuditAction.Inbound"] = "入库",

        ["AuditAction.MoveLocation"] = "移库",

        ["AuditAction.SendToRepair"] = "送修",

        ["AuditAction.ReceiveFromRepair"] = "维修入库",

        ["AuditAction.BorrowLend"] = "借出",

        ["AuditAction.BorrowReturn"] = "归还",

        ["AuditAction.Adjustment"] = "调整",

        ["AuditAction.InventoryCheck"] = "盘点",

        ["AuditAction.ImportOperation"] = "导入数据",

        ["AuditAction.Create"] = "新建",

        ["AuditAction.Edit"] = "编辑",

        ["AuditAction.Rebuild"] = "重建",

        ["AuditAction.Reverse"] = "反转",

        ["AuditAction.Delete"] = "删除",

        ["AuditAction.Update"] = "更新",

        ["AuditAction.SoftDelete"] = "停用",

        ["AuditAction.Restore"] = "恢复",

        ["AuditAction.HardDelete"] = "永久删除",



        ["AuditEntity.InboundDocument"] = "入库单",

        ["AuditEntity.MoveDocument"] = "移库单",

        ["AuditEntity.RepairDocument"] = "维修单",

        ["AuditEntity.BorrowDocument"] = "借用单",

        ["AuditEntity.AdjustmentDocument"] = "调整单",

        ["AuditEntity.InventoryCheckDocument"] = "盘点单",

        ["AuditEntity.Item"] = "物料",

        ["AuditEntity.ItemCategory"] = "物料组",

        ["AuditEntity.ExternalParty"] = "外部对象",

        ["AuditEntity.BinLocation"] = "库位",

        ["AuditEntity.SystemUser"] = "账号",

        ["AuditEntity.ImportBatch"] = "导入批次",



        ["Enum.ItemStatus.InStock"] = "在库",

        ["Enum.ItemStatus.Normal"] = "通过",

        ["Enum.ItemStatus.Reserved"] = "已预留",

        ["Enum.ItemStatus.Repairing"] = "维修中",

        ["Enum.ItemStatus.LentOut"] = "已借出",

        ["Enum.ItemStatus.Returned"] = "已归还",

        ["Enum.ItemStatus.Damaged"] = "损坏",

        ["Enum.ItemStatus.Lost"] = "丢失",

        ["Enum.ItemStatus.Disposed"] = "已报废",

        ["Enum.ItemStatus.InTransit"] = "运输中",

        ["Enum.ItemStatus.Replacement"] = "替换",

        ["Enum.ItemStatus.Matched"] = "一致",

        ["Enum.ItemStatus.Missing"] = "缺失",

        ["Enum.ItemStatus.Extra"] = "多余",

        ["Enum.ItemStatus.WrongLocation"] = "位置错误",



        ["Enum.InventoryCheckLineResult.Matched"] = "一致",

        ["Enum.InventoryCheckLineResult.Missing"] = "缺失",

        ["Enum.InventoryCheckLineResult.Extra"] = "多出",

        ["Enum.InventoryCheckLineResult.WrongLocation"] = "位置错误",

        ["Enum.InventoryCheckLineResult.Damaged"] = "损坏",



        ["Enum.RepairResult.Success"] = "维修成功",

        ["Enum.RepairResult.Failed"] = "维修失败",

        ["Enum.RepairResult.Replaced"] = "已更换",

        ["Enum.BorrowReturnCondition.Normal"] = "正常",

        ["Enum.BorrowReturnCondition.Damaged"] = "损坏",

        ["Enum.BorrowReturnCondition.Lost"] = "丢失",

        ["Enum.BorrowReturnCondition.Scrapped"] = "报废",



        ["Enum.ExternalPartyType.Supplier"] = "供应商",

        ["Enum.ExternalPartyType.RepairVendor"] = "维修商",

        ["Enum.ExternalPartyType.Borrower"] = "借用人",

        ["Enum.ExternalPartyType.Customer"] = "客户",

        ["Enum.ExternalPartyType.Department"] = "部门",

        ["Enum.ExternalPartyType.Employee"] = "员工",

        ["Enum.ExternalPartyType.Logistics"] = "物流",

        ["Enum.ExternalPartyType.Approver"] = "审批人",
        ["Enum.ExternalPartyType.DepartmentOwner"] = "仓库部门主管",


        ["Enum.MovementActionType.Inbound"] = "入库",

        ["Enum.MovementActionType.MoveLocation"] = "移库",

        ["Enum.MovementActionType.SendToRepair"] = "送修",

        ["Enum.MovementActionType.ReceiveFromRepair"] = "维修入库",

        ["Enum.MovementActionType.Lend"] = "借出",

        ["Enum.MovementActionType.ReturnBorrowed"] = "归还",

        ["Enum.MovementActionType.Adjustment"] = "调整",

        ["Enum.MovementActionType.InventoryCheck"] = "盘点",

        ["Enum.MovementActionType.ImportOpening"] = "期初导入",

        ["Enum.MovementActionType.Dispose"] = "报废",

        ["Enum.MovementActionType.Transfer"] = "调拨",



        ["Enum.ImportBatchStatus.Uploaded"] = "已上传",

        ["Enum.ImportBatchStatus.Validated"] = "已校验",

        ["Enum.ImportBatchStatus.Blocked"] = "已阻止",

        ["Enum.ImportBatchStatus.Confirmed"] = "已确认",

        ["Enum.ImportBatchStatus.Failed"] = "失败",



        ["Enum.ValidationSeverity.Info"] = "信息",

        ["Enum.ValidationSeverity.Warning"] = "警告",

        ["Enum.ValidationSeverity.Blocking"] = "阻断错误",



        ["Enum.InventoryTransactionType.Inbound"] = "入库",

        ["Enum.InventoryTransactionType.Move"] = "移动",

        ["Enum.InventoryTransactionType.RepairSend"] = "送修",

        ["Enum.InventoryTransactionType.RepairReceive"] = "维修入库",

        ["Enum.InventoryTransactionType.BorrowLend"] = "借出",

        ["Enum.InventoryTransactionType.BorrowReturn"] = "归还",

        ["Enum.InventoryTransactionType.Adjustment"] = "调整",

        ["Enum.InventoryTransactionType.InventoryCheck"] = "盘点",



        ["Enum.LocationType.Warehouse"] = "仓库",

        ["Enum.LocationType.BinLocation"] = "库位",

        ["Enum.LocationType.RepairVendor"] = "维修商",

        ["Enum.LocationType.Borrower"] = "借用人",

        ["Enum.LocationType.Disposed"] = "已报废",

        ["Enum.LocationType.Unknown"] = "未知",

        ["Enum.LocationType.Supplier"] = "供应商",

        ["Enum.LocationType.Logistics"] = "物流",



        ["Enum.InventoryTransactionType.OpeningBalance"] = "期初余额",



        ["Enum.DocumentStatus.Draft"] = "草稿",

        ["Enum.DocumentStatus.Posted"] = "已过账",

        ["Enum.DocumentStatus.CancelledByAdjustment"] = "已通过调整取消",



        ["ImportType.ItemMaster"] = "物料目录",

        ["ImportType.WarehouseStructure"] = "仓库结构",

        ["ImportType.Inbound"] = "入库",

        ["ImportType.InventoryCheck"] = "库存盘点",

        ["ImportType.RepairSend"] = "送修出库",

        ["ImportType.BorrowLend"] = "借用出库",



        ["Enum.InventoryStatus.InStock"] = "在库",

        ["Enum.InventoryStatus.Normal"] = "正常",

        ["Enum.InventoryStatus.Damaged"] = "损坏",

        ["Enum.InventoryStatus.Scrapped"] = "报废",

        ["Enum.InventoryStatus.Replacement"] = "替换",



        ["Enum.ItemStatus.Scrapped"] = "报废",

        ["Enum.ItemStatusView.InStock"] = "在库",

        ["Enum.ItemStatusView.Normal"] = "正常",

        ["Enum.ItemStatusView.Repairing"] = "维修中",

        ["Enum.ItemStatusView.LentOut"] = "已借出",

        ["Enum.ItemStatusView.Damaged"] = "损坏",

        ["Enum.ItemStatusView.Lost"] = "丢失",

        ["Enum.ItemStatusView.Disposed"] = "已报废",

        ["Enum.ItemStatusView.Replacement"] = "替换",

        ["Enum.ItemStatusView.Scrapped"] = "报废",



        ["Enum.DocumentPeriodType.Week"] = "周",

        ["Enum.DocumentPeriodType.Month"] = "月",

        ["Enum.DocumentPeriodType.Quarter"] = "季度",

        ["Enum.DocumentPeriodType.Year"] = "年度",

        ["DocumentPeriodType"] = "盘点周期",



        // Alias (raw enum name fallback)

        ["InboundReceive"] = "入库收货",

        ["BorrowIssue"] = "借出发料",

        ["BorrowReturn"] = "归还入库",

        ["RepairSend"] = "送修出库",

        ["RepairReceive"] = "维修入库",



        // Missing UI keys

        ["By"] = "由",

        ["History & Timeline"] = "历史与时间线",

        ["Old Status"] = "原状态",

        ["New Status"] = "新状态",

        ["Old Location"] = "原位置",

        ["New Location"] = "新位置",



        ["Inventory movement history shows the historical transactions of an item instance."] = "库存移动历史显示物料实例的历史交易记录。",

        ["Inventory preview shows the current status and location of an item instance."] = "库存预览显示物料实例的当前状态和位置。",

        ["Serial-managed items require serial numbers for tracking individual instances."] = "序列号管理的物料需要序列号来跟踪每个实例。",

        ["Active/inactive status is determined by the presence of an active record in the database."] = "启用/停用状态由数据库中是否存在有效记录决定。",

        ["Supplier, borrower, and repair vendor information is stored as external parties in the system."] = "供应商、借用人和维修商信息在系统中作为外部对象存储。",

        ["Item and resource translations are provided for multilingual support in the user interface."] = "物料和界面资源提供多语言翻译支持用户界面。",

        ["Serial tracking allows monitoring the movement and status of individual item instances through their unique serial numbers."] = "序列号跟踪允许通过唯一的序列号监控每个物料实例的移动和状态。",

        ["Language coverage indicates the availability of translations for different languages in the system."] = "语言覆盖表示系统中不同语言翻译的可用性。",

        ["Permission checks are enforced for warehouse operations to ensure users have appropriate access rights."] = "仓库操作执行权限检查以确保用户具有适当的访问权限。",

        ["Import functionality allows bulk data entry for items, warehouses, and transactions using predefined templates."] = "导入功能允许使用预定义模板批量输入物料、仓库和交易数据。",

        ["Audit logs capture user activities and changes to critical entities for accountability and traceability."] = "审计日志记录用户活动和关键实体的更改，以实现问责制和可追溯性。",

        ["Notifications provide alerts for important events such as stocktake schedules and document approvals."] = "通知提供重要事件的提醒，如盘点计划和单据审批。",

        ["Users must have the necessary permissions for the warehouses involved in the operation."] = "用户必须拥有涉及操作的仓库的必要权限。",

        ["Permission checks cover actions such as inbound, move location, repair send, inventory check, and adjustment."] = "权限检查涵盖入库、移动位置、发送维修、库存检查和调整等操作。",

        ["Permission checks are performed at both the document level and line item level to ensure comprehensive access control."] = "权限检查在单据级别和行项目级别进行，以确保全面的访问控制。",

        ["Users without the required permissions will receive error messages when attempting to perform restricted operations."] = "没有必要权限的用户在尝试执行受限操作时将收到错误消息。",

        ["Warehouse permissions are assigned to users based on their roles and responsibilities within the organization."] = "仓库权限根据用户在组织中的角色和职责分配。",

        ["Regular reviews of user permissions are recommended to maintain security and ensure that access rights are up to date."] = "建议定期审查用户权限以维护安全性并确保访问权限是最新的。",

        ["The history includes details such as transaction type, date and time, quantity, source and destination locations, and the user who performed the transaction."] = "历史记录包括交易类型、日期和时间、数量、来源和目标位置以及执行交易的用户等详细信息。",

        ["Inventory movement history provides insights into the lifecycle of an item instance, including when it was received, moved, sent for repair, lent out, returned, adjusted, or checked during inventory."] = "库存移动历史提供了物料实例生命周期的洞察，包括何时接收、移动、送修、借出、归还、调整或盘点。",

        ["Users can access inventory movement history through the system interface to track the status and location changes of item instances over time."] = "用户可以通过系统界面访问库存移动历史，以跟踪物料实例随时间的状态和位置变化。",


        ["Only permanently delete items with no transaction history."] = "仅永久删除未产生业务记录的物料。",

        ["Receiver Phone"] = "收货人电话",

        ["Receiver Department"] = "收货部门",

        ["Receiver Name"] = "收货人姓名",

        ["Receiver Code"] = "录入人编码",

        ["Receiver"] = "收货人",

        ["Receiver is required."] = "必须填写收货人信息。",

        ["Warehouse B34G"] = "B34G 仓库",
        ["B34G Warehouse"] = "B34G 仓库",
        ["Warehouse Operations Portal"] = "仓库运营门户",



        // ─── Document Log action keys (Repair + Adjustment) ──────────────────

        ["Adjust"] = "库存调整",

        ["Replace-Out"] = "替换出库",

        ["Replace-In"] = "替换入库",

        ["Repair Vendor"] = "维修商",

        ["Repair Result Note"] = "维修结果备注",

        ["Performed By"] = "操作人员",

        ["Action Type"] = "操作类型",



        // ─── InventoryCheck session-based ────────────────────────────────────

        ["Session Status"] = "盘点状态",

        ["Draft"] = "草稿",

        ["InProgress"] = "盘点中",

        ["Finalized"] = "已完成",

        ["Create Session"] = "创建盘点",

        ["Scan Batch"] = "批量扫描",

        ["Finalize Session"] = "完成盘点",

        ["Session Progress"] = "盘点进度",

        ["Total Scanned"] = "已扫描总数",

        ["Total Missing"] = "缺失总数",

        ["Batch Matched"] = "吻合（本批）",

        ["Batch Wrong Location"] = "位置错误（本批）",

        ["Batch Extra"] = "多出（本批）",

        ["Batch Skipped"] = "跳过（本批）",

        ["Inventory check session created."] = "盘点会话已创建。",

        ["Inventory check session has already been finalized."] = "盘点会话已完成。",

        ["Inventory check document not found."] = "未找到盘点单据。",

        ["This inventory check session has already been finalized."] = "此盘点会话已完成。",

        ["At least one scan line is required."] = "至少需要一条扫描记录。",

        ["Inventory check finalized."] = "盘点已完成。",
        ["Lines appended to existing repair document."] = "明细已添加到现有维修单。",

        ["Sender Code"] = "发件人编码",
        ["Sender Name"] = "发件人名称",
        ["Sender Phone"] = "发件人电话",
        ["Adjustment Type"] = "调整类型",
        ["Increase"] = "增加",
        ["Decrease"] = "减少",

        ["Repair Document No"] = "维修单号",

        ["Exited edit mode"] = "已退出编辑模式",
        ["info"] = "信息",
        ["Cancel Edit"] = "取消",
        ["No blocking dependency found."] = "未找到阻塞依赖项",
        ["Document has blocking dependencies."] = "单据存在阻塞依赖项",
        ["Unsupported document type '{0}'."] = "不支持的单据类型 '{0}'",
        ["Document not found."] = "未找到单据",
        ["Rebuild"] = "重建",
        ["Rebuild Effects"] = "重建影响",
        ["Reverse"] = "反转",
        ["Delete"] = "删除",
        ["Document effects reversed before delete."] = "删除前已反转单据影响",
        ["Document deleted."] = "单据已删除",
        ["Invalid borrow return payload."] = "借还数据无效",
        ["Document edited and effects rebuilt."] = "单据已编辑并重建影响",
        ["Document updated."] = "单据已更新",
        ["Borrow return updated."] = "借还单已更新",
        ["Repair receive updated."] = "维修收回单已更新",
        ["Dependency Warning"] = "依赖警告",
        ["Review dependency impact before continuing."] = "继续之前请检查依赖影响",
        ["Close"] = "关闭",
        ["Blocked reason"] = "阻塞原因",
        ["Item instance {0} has downstream operations."] = "物料实例 {0} 存在下游操作",
        ["Item instance {0} has downstream operations after {1}."] = "物料实例 {0} 在步骤 {1} 之后存在下游操作",
        ["Quantity item {0}/{1} has later quantity transactions."] = "数量物料 {0}/{1} 存在更晚的数量交易",
        ["Current persisted payload will be replayed to rebuild effects."] = "当前已保存的数据将重新执行以重建影响",
        ["This will reverse and replay the current document effects."] = "此操作将反转并重新执行当前单据影响",
        ["Document will be reversed and deleted transactionally."] = "单据将在同一事务中反转并删除",
        ["This document will be reversed and deleted."] = "此单据将被反转并删除",
        ["Document Type"] = "单据类型",
        ["Editing document"] = "正在编辑单据",
        ["Save Changes"] = "保存",
        ["Inbound document not found."] = "未找到入库单",
        ["Move document not found."] = "未找到移库单",
        ["Adjustment document not found."] = "未找到调整单",
        ["Quantity inventory document not found."] = "未找到数量库存单",
        ["Borrow document has no return effects to delete."] = "借用单没有可删除的归还影响",
        ["Repair document has no receive effects to delete."] = "维修单没有可删除的收回影响",
        ["Cannot delete borrow lend after return has been posted. Delete borrow return first."] = "归还已过账后不能删除借出单。请先删除借还单",
        ["Cannot delete repair send after repair receive has been posted. Delete repair receive first."] = "维修收回已过账后不能删除送修单。请先删除维修收回单",
        ["Cannot edit or delete borrow lend after return has been posted. Delete borrow return first."] = "归还已过账后不能编辑或删除借出单。请先删除借还单",
        ["Cannot edit or delete repair send after repair receive has been posted. Delete repair receive first."] = "维修收回已过账后不能编辑或删除送修单。请先删除维修收回单",
        ["Receive"] = "接收",
        ["Success"] = "成功",
        ["Quantity document edited: old document deleted and effects rebuilt from new payload."] = "数量盘点单已编辑：旧单据已删除，并根据新数据重建影响",
        ["Normal edit no longer performs full delete/repost rebuild. Line-level changes for this document type require selective mutation support; use explicit Rebuild only for recovery/full replay."] = "普通编辑不再执行完整删除/重建。此类单据的行级变更需要支持选择性变更；仅在恢复或完整重放时使用显式 Rebuild",
        ["Document header edited without rebuilding effects."] = "单据头已编辑且未重建影响",

        // ─── Reconciliation Audit module ──────────────────────────────────────

        ["Reconciliation"] = "对账",

        ["recon.title"] = "资产对账",

        ["recon.reflist"] = "参考列表",

        ["recon.sessions"] = "对账会话",

        ["recon.results"] = "对账结果",

        ["recon.newlist"] = "新建列表",

        ["recon.newsession"] = "新建会话",

        ["recon.import"] = "导入",

        ["recon.runaudit"] = "运行对账",

        ["recon.listcode"] = "列表编码",

        ["recon.listname"] = "列表名称",

        ["recon.description"] = "描述",

        ["recon.itemcount"] = "行数",

        ["recon.importmode"] = "导入模式",

        ["recon.supplement"] = "追加（Upsert）",

        ["recon.replace"] = "替换（删除并重导）",

        ["recon.matched"] = "匹配",

        ["recon.erponly"] = "仅存在于系统中",

        ["recon.refonly"] = "仅存在于参考列表中",

        ["recon.lentout"] = "借出中",

        ["recon.resolved"] = "已在系统中确认",

        ["recon.sessionno"] = "会话编号",

        ["recon.status.draft"] = "草稿",

        ["recon.status.running"] = "运行中",

        ["recon.status.completed"] = "已完成",

        ["recon.status.archived"] = "已归档",

        ["recon.export"] = "导出Excel",

        ["recon.template"] = "下载模板",

        ["recon.allresults"] = "全部结果",

        ["recon.filter"] = "筛选",

        ["recon.total"] = "合计",

        ["recon.importinfo"] = "Excel文件必须包含ItemCode和SerialNumber列。",

        ["recon.confirmrun"] = "运行此会话的对账？",

        ["recon.running"] = "对账运行中...",

        ["recon.nolists"] = "请先创建参考列表。",

        ["recon.noitems"] = "尚未导入数据。",

        ["recon.nosessions"] = "尚无对账会话。",

        ["recon.noresults"] = "没有结果。",

        ["recon.home"] = "对账",

        // ── Phase 5: Import Types (zh) ──
        ["ImportType.ItemMaster"] = "物料目录",
        ["ImportType.WarehouseStructure"] = "仓库结构",
        ["ImportType.Inbound"] = "入库",
        ["ImportType.InventoryCheck"] = "盘点",
        ["ImportType.RepairSend"] = "送修",
        ["ImportType.BorrowLend"] = "借出",
        ["ImportType.QuantityInbound"] = "数量入库",
        ["ImportType.QuantityOutbound"] = "数量出库",
        ["ImportType.QuantityAdjust"] = "数量调整",
        ["ImportType.MoveLocation"] = "移库",
        ["ImportType.BorrowReturn"] = "归还",
        ["ImportType.RepairReceive"] = "维修入库",

        // ── Phase 5: Column headers (zh) ──
        ["OwnerName"] = "所有人",
        ["TrackingType"] = "跟踪类型",
        ["SnCode"] = "批次/序列号",
        ["Quantity"] = "数量",
        ["ItemCategoryCode"] = "物料组代码",
        ["SourceWarehouseCode"] = "源仓库",
        ["TargetWarehouseCode"] = "目标仓库",
        ["TargetBinCode"] = "目标库位",
        ["BorrowDocumentNo"] = "借用单号",
        ["RepairDocumentNo"] = "维修单号",
        ["ReturnLocationBinCode"] = "归还库位",
        ["NewStatus"] = "新状态",
        //["MT"] = "型号",
        ["Condition"] = "状态",
        ["BorrowerCode"] = "借用人代码",
        ["BorrowerName"] = "借用人姓名",
        ["BorrowDate"] = "借用日期",
        ["DueDate"] = "到期日",
        ["BorrowDepartment"] = "借用部门",
        ["BorrowerPhone"] = "借用人电话",
        ["DepartmentOwner"] = "部门负责人",
        ["RepairVendorCode"] = "维修商代码",
        ["ExpectedReturnDate"] = "预计回收日期",
        ["TargetExternalLocation"] = "外部地点",

        // ── Phase 5: Dashboard labels (zh) ──
        ["Quantity Inventory Summary"] = "数量库存汇总",
        ["Total Quantity"] = "总数量",
        ["Active SNs"] = "PN",
        ["Owners"] = "所有人数",
        ["Total SN Lots"] = "PN总数",
        ["Quantity by Owner"] = "按所有人汇总",
        ["Quantity by Item"] = "按物料汇总",
        ["Inventory Quantity Distribution by ItemCode"] = "按物料编码的库存数量分布",
        ["Inventory Quantity Distribution by ItemCategory"] = "按物料组的库存数量分布",
        ["Percentage"] = "百分比",

        // ── Phase 5: Export sheet names (zh) ──
        ["InboundDocuments"] = "入库单",
        ["QuantityBalance"] = "数量库存",
        ["BorrowDocuments"] = "借用单",
        ["RepairDocuments"] = "维修单",
        ["MoveDocuments"] = "移库单",
        ["AdjustmentDocuments"] = "调整单",
        ["InventoryCheckDocuments"] = "盘点单",
        ["QuantityTransactions"] = "数量事务",

    };

}



