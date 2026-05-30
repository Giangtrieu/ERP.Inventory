using ERP.Inventory.Application.Common;
using ERP.Inventory.Application.DTOs;
using ERP.Inventory.Application.Interfaces;
using ERP.Inventory.Domain.Entities;
using ERP.Inventory.Domain.Enums;
using ERP.Inventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Globalization;

namespace ERP.Inventory.Infrastructure.Services;

/// <summary>
/// Shared helpers for all inventory operation services. Extracted from the original
/// monolithic InventoryOperationService to eliminate duplication.
/// </summary>
public abstract class InventoryOperationBase
{
    protected readonly InventoryDbContext _db;
    protected readonly IDocumentNumberService _documentNumbers;
    protected readonly IDateTimeProvider _clock;

    protected InventoryOperationBase(InventoryDbContext db, IDocumentNumberService documentNumbers, IDateTimeProvider clock)
    {
        _db = db;
        _documentNumbers = documentNumbers;
        _clock = clock;
    }

    protected async Task<OperationTransactionScope> BeginOperationTransactionAsync(CancellationToken cancellationToken)
    {
        if (_db.Database.CurrentTransaction != null)
        {
            return new OperationTransactionScope(null, ownsTransaction: false);
        }

        var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        return new OperationTransactionScope(transaction, ownsTransaction: true);
    }

    // ─── Code → ID resolution helpers ────────────────────────
    protected Task<Warehouse?> FindWarehouseByIDAsync(int Id, CancellationToken ct)
       => _db.Warehouses.AsNoTracking().FirstOrDefaultAsync(x => x.Id == Id && x.IsActive, ct);

    protected Task<Warehouse?> FindWarehouseByCodeAsync(string? code, CancellationToken ct)
        => string.IsNullOrWhiteSpace(code) ? Task.FromResult<Warehouse?>(null)
            : _db.Warehouses.AsNoTracking().FirstOrDefaultAsync(x => x.WarehouseCode == code.Trim() && x.IsActive, ct);

    protected Task<BinLocation?> FindBinByCodeAsync(string? binCode, CancellationToken ct)
        => string.IsNullOrWhiteSpace(binCode) ? Task.FromResult<BinLocation?>(null)
            : _db.BinLocations.AsNoTracking().FirstOrDefaultAsync(x => x.BinCode == binCode.Trim() && x.IsActive, ct);

    protected Task<BinLocation?> FindBinByIdAsync(int? binId, CancellationToken ct)
    => binId == 0 ? Task.FromResult<BinLocation?>(null)
        : _db.BinLocations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == binId && x.IsActive, ct);

    protected Task<Item?> FindItemByCodeAsync(string? itemCode, CancellationToken ct)
        => string.IsNullOrWhiteSpace(itemCode) ? Task.FromResult<Item?>(null)
            : _db.Items.AsNoTracking().FirstOrDefaultAsync(x => x.ItemCode == itemCode.Trim() && x.IsActive, ct);
    protected Task<ItemCategory?> FindItemCategoryByCodeAsync(string? itemCategoryCode, CancellationToken ct)
        => string.IsNullOrWhiteSpace(itemCategoryCode) ? Task.FromResult<ItemCategory?>(null)
            : _db.ItemCategories.AsNoTracking().FirstOrDefaultAsync(x => x.CategoryCode == itemCategoryCode.Trim() && x.IsActive, ct);

    protected Task<InboundDocument?> FindInboundDocumentByCodeAsync(string? documentNo, CancellationToken ct)
       => string.IsNullOrWhiteSpace(documentNo) ? Task.FromResult<InboundDocument?>(null)
           : _db.InboundDocuments.AsNoTracking().FirstOrDefaultAsync(x => x.DocumentNo == documentNo.Trim(), ct);
    protected Task<BorrowDocument?> FindBorrowLendDocumentByCodeAsync(string? documentNo, CancellationToken ct)
       => string.IsNullOrWhiteSpace(documentNo) ? Task.FromResult<BorrowDocument?>(null)
           : _db.BorrowDocuments.AsNoTracking().FirstOrDefaultAsync(x => x.DocumentNo == documentNo.Trim(), ct);

    protected async Task<ExternalParty> GetOrCreatePartyByNameAsync(string name, string code, ExternalPartyType type,string codePrefix, string phone, string userName, DateTime now,CancellationToken cancellationToken)
    {
        var party = await FindPartyByNameAsync(name, type, cancellationToken);
        if (party != null)return party;

        party = new ExternalParty
        {
            PartyCode = string.IsNullOrEmpty(code) ? _documentNumbers.Next(codePrefix, now) : code,
            Name = name,
            PartyType = type,
            Phone = phone,
            CreatedBy = userName
        };

        _db.ExternalParties.Add(party);

        return party;
    }

    protected async Task<string> NextCheckNoAsync(DateTime sessionDate, DocumentPeriodType periodType, CancellationToken cancellationToken = default)
    {
        string documentNo;

        switch (periodType)
        {
            case DocumentPeriodType.Week:
                {
                    var calendar = CultureInfo.InvariantCulture.Calendar;

                    var week = calendar.GetWeekOfYear(
                        sessionDate,
                        CalendarWeekRule.FirstFourDayWeek,
                        DayOfWeek.Monday);

                    documentNo = $"CHK/{sessionDate:yyyy}/W{week:D2}";
                    break;
                }

            case DocumentPeriodType.Month:
                {
                    documentNo = $"CHK/{sessionDate:yyyy}/M{sessionDate.Month:D2}";
                    break;
                }

            case DocumentPeriodType.Quarter:
                {
                    var quarter = (sessionDate.Month - 1) / 3 + 1;

                    documentNo = $"CHK/{sessionDate:yyyy}/Q{quarter}";
                    break;
                }

            case DocumentPeriodType.Year:
                {
                    documentNo = $"CHK/{sessionDate:yyyy}";
                    break;
                }

            default:
                throw new ArgumentOutOfRangeException(nameof(periodType), periodType, null);
        }

        return documentNo;
    }
    /// <summary>Find item instance by ItemCode + SerialNumber (unique combination per business rule).</summary>
    protected async Task<ItemInstance?> FindInstanceByCodeAsync(string? itemCode, string? serialNumber, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(itemCode) || string.IsNullOrWhiteSpace(serialNumber))
            return null;
        var code = itemCode.Trim();
        var sn = serialNumber.Trim();
        return await _db.ItemInstances.Include(x => x.Item)
            .FirstOrDefaultAsync(x => x.Item != null && x.Item.ItemCode == code && x.SerialNumber == sn, ct);
    }

    protected Task<ExternalParty?> FindPartyByCodeAsync(string? partyCode, ExternalPartyType partyType, CancellationToken ct)
        => string.IsNullOrWhiteSpace(partyCode) ? Task.FromResult<ExternalParty?>(null)
            : _db.ExternalParties.AsNoTracking().FirstOrDefaultAsync(x => x.PartyCode == partyCode.Trim() && x.PartyType == partyType && x.IsActive, ct);

    protected Task<ExternalParty?> FindPartyByNameAsync(string? name, ExternalPartyType partyType, CancellationToken ct)
       => string.IsNullOrWhiteSpace(name) ? Task.FromResult<ExternalParty?>(null)
           : _db.ExternalParties.AsNoTracking().FirstOrDefaultAsync(x => x.Name == name.Trim() && x.PartyType == partyType && x.IsActive, ct);

    protected static PostedDocumentDto ToPostedDto(string documentType, int documentId, string documentNo, DateTime postedAt)
    {
        return new PostedDocumentDto { DocumentType = documentType, DocumentId = documentId, DocumentNo = documentNo, PostedAt = postedAt };
    }

    protected async Task<CurrentItemLocation> GetCurrentLocationAsync(int itemInstanceId, CancellationToken cancellationToken)
    {
        var current = await _db.CurrentItemLocations
            .Include(x => x.BinLocation)
            .Include(x => x.ExternalParty)
            .Include(x => x.Warehouse)
            .FirstOrDefaultAsync(x => x.ItemInstanceId == itemInstanceId, cancellationToken);
        if (current == null)
            throw new InvalidOperationException($"Current location for item instance {itemInstanceId} does not exist.");
        return current;
    }

    protected async Task<bool> BinHasActiveItemAsync(int binLocationId, int? exceptItemInstanceId, CancellationToken cancellationToken)
    {
        return await _db.CurrentItemLocations.AsNoTracking()
            .AnyAsync(x =>
                x.BinLocationId == binLocationId &&
                (!exceptItemInstanceId.HasValue || x.ItemInstanceId != exceptItemInstanceId.Value) &&
                x.ItemInstance != null &&
                x.ItemInstance.IsActive &&
                x.ItemInstance.Status != ItemStatus.Lost &&
                x.ItemInstance.Status != ItemStatus.Disposed,
                cancellationToken);
    }

    protected static string LocationDisplay(CurrentItemLocation location)
    {
        if (location.BinLocation != null) return location.BinLocation.FullPath;
        if (!string.IsNullOrWhiteSpace(location.ExternalLocationText))
            return location.ExternalParty != null ? ExternalLocationDisplay(location.ExternalParty.Name, location.ExternalLocationText) : location.ExternalLocationText;
        if (location.ExternalParty != null) return location.ExternalParty.Name;
        if (location.Warehouse != null) return location.Warehouse.Name;
        if (!string.IsNullOrWhiteSpace(location.ReferenceDocumentNo)) return $"{location.LocationType} / {location.ReferenceDocumentNo}";
        return location.LocationType.ToString();
    }

    protected static string? NormalizeExternalLocation(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    protected static string ExternalLocationDisplay(string partyName, string externalLocation)
        => string.IsNullOrWhiteSpace(partyName) ? externalLocation : $"{partyName} - {externalLocation}";

    protected async Task ApplyStockDeltaAsync(int warehouseId, int? binLocationId, int itemId, ItemStatus status, decimal delta, CurrentUserContext user, CancellationToken cancellationToken)
    {
        if (delta == 0) return;
        var now = _clock.UtcNow;

        var balance = _db.StockBalances.Local.FirstOrDefault(x =>x.WarehouseId == warehouseId &&
        x.BinLocationId == binLocationId && x.ItemId == itemId && x.Status == status);

        if (balance == null)
        {
            balance = await _db.StockBalances.FirstOrDefaultAsync(x => x.WarehouseId == warehouseId && 
            x.BinLocationId == binLocationId && x.ItemId == itemId && x.Status == status, cancellationToken);
        }

        if (balance == null)
        {
            balance = new StockBalance { WarehouseId = warehouseId, BinLocationId = binLocationId, ItemId = itemId, Status = status, Quantity = 0, CreatedAt = now, CreatedBy = user.UserName };
            _db.StockBalances.Add(balance);
        }

        balance.Quantity += delta;
        balance.UpdatedAt = now;
        balance.UpdatedBy = user.UserName;
    }

    protected void AddHistory(int itemInstanceId, MovementActionType action, LocationType? fromType, int? fromId, string? fromDisplay, LocationType? toType, int? toId, string? toDisplay, ItemStatus oldStatus, ItemStatus newStatus, string documentType, int documentId, string documentNo, string? note, CurrentUserContext user, Guid? lifecycleBatchId = null)
    {
        _db.ItemMovementHistories.Add(new ItemMovementHistory
        {
            ItemInstanceId = itemInstanceId, ActionType = action,
            FromLocationType = fromType, FromLocationId = fromId, FromLocationDisplay = fromDisplay,
            ToLocationType = toType, ToLocationId = toId, ToLocationDisplay = toDisplay,
            OldStatus = oldStatus, NewStatus = newStatus,
            DocumentType = documentType, DocumentId = documentId, DocumentNo = documentNo,
            LifecycleBatchId = lifecycleBatchId,
            Note = note, PerformedAt = _clock.UtcNow, PerformedBy = user.UserName
        });
    }

    protected void AddInventoryTransaction(InventoryTransactionType type, int itemId, int? itemInstanceId, int? warehouseId, int? binLocationId, decimal quantityDelta, ItemStatus statusAfter, string documentType, int documentId, string documentNo, CurrentUserContext user, Guid? lifecycleBatchId = null)
    {
        _db.InventoryTransactions.Add(new InventoryTransaction
        {
            TransactionType = type, ItemId = itemId, ItemInstanceId = itemInstanceId,
            WarehouseId = warehouseId, BinLocationId = binLocationId,
            QuantityDelta = quantityDelta, StatusAfter = statusAfter,
            DocumentType = documentType, DocumentId = documentId, DocumentNo = documentNo,
            LifecycleBatchId = lifecycleBatchId,
            PostedAt = _clock.UtcNow, PostedBy = user.UserName
        });
    }

    protected void AddPostSideEffects(string action, string entityName, int entityId, string documentNo, CurrentUserContext user, string message)
    {
        var now = _clock.UtcNow;
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = user.UserId, UserName = user.UserName, Action = action, EntityName = entityName,
            EntityId = entityId, ReferenceNo = documentNo, Result = "Success", CreatedAt = now
        });

        if (!string.IsNullOrWhiteSpace(user.UserId))
        {
            _db.Notifications.Add(new Notification
            {
                UserId = user.UserId,
                Title = NotifyText("Document posted"),
                Message_Vi = $"{NotifyText(message, "vi")} {documentNo}",
                Message_En = $"{NotifyText(message)} {documentNo}",
                Message_Zh = $"{NotifyText(message, "zh")} {documentNo}",
                LinkUrl = $"/?screen=tracking&documentNo={Uri.EscapeDataString(documentNo)}",
                CreatedAt = now, CreatedBy = user.UserName
            });
        }
    }

    protected static string NotifyText(string key, string language = "en")
    {
        return NotificationResources.TryGetValue(language, out var resources) && resources.TryGetValue(key, out var value) ? value : key;
    }

    protected static readonly Dictionary<string, Dictionary<string, string>> NotificationResources = new()
    {
        ["vi"] = new()
        {
            ["Document posted"] = "Chứng từ đã ghi sổ",
            ["Inbound posted."] = "Đã ghi sổ nhập kho.",
            ["Move posted."] = "Đã ghi sổ chuyển vị trí.",
            ["Repair send posted."] = "Đã ghi sổ gửi sửa chữa.",
            ["Repair receive posted."] = "Đã ghi sổ nhận sửa chữa.",
            ["Borrow lend posted."] = "Đã ghi sổ phiếu mượn.",
            ["Borrow return posted."] = "Đã ghi sổ phiếu trả.",
            ["Adjustment posted."] = "Đã ghi sổ điều chỉnh.",
            ["Quantity inventory posted."] = "Đã ghi sổ tồn kho số lượng.",
            ["Inventory check posted."] = "Đã ghi sổ kiểm kê.",
            ["Inventory check result"] = "Kết quả kiểm kê",
            ["Inventory check action required"] = "Kiểm kê cần xử lý",
            ["Inventory check completed without discrepancy."] = "Kiểm kê hoàn tất, không phát hiện chênh lệch.",
            ["Inventory check completed with discrepancies."] = "Kiểm kê phát hiện chênh lệch.",
            ["InventoryCheck.Missing"] = "Thiếu hàng", ["InventoryCheck.Extra"] = "Thừa hàng",
            ["InventoryCheck.WrongLocation"] = "Sai vị trí", ["InventoryCheck.Damaged"] = "Hư hỏng",
            ["InventoryCheck.Guidance.Missing"] = "Thiếu hàng: kiểm tra lại khu vực, lịch sử xuất nhập và báo quản lý nếu không tìm thấy.",
            ["InventoryCheck.Guidance.Extra"] = "Thừa hàng: xác minh nguồn phát sinh rồi tạo nhập kho hoặc điều chỉnh nếu hợp lệ.",
            ["InventoryCheck.Guidance.WrongLocation"] = "Sai vị trí: đưa hàng về đúng vị trí hoặc tạo phiếu chuyển vị trí để đồng bộ hệ thống.",
            ["InventoryCheck.Guidance.Damaged"] = "Hư hỏng: cách ly hàng, tạo phiếu sửa chữa hoặc điều chỉnh trạng thái."
        },
        ["en"] = new()
        {
            ["Inventory check result"] = "Inventory check result",
            ["Inventory check action required"] = "Inventory check action required",
            ["Inventory check completed without discrepancy."] = "Inventory check completed without discrepancy.",
            ["Inventory check completed with discrepancies."] = "Inventory check completed with discrepancies.",
            ["Quantity inventory posted."] = "Quantity inventory posted.",
            ["InventoryCheck.Missing"] = "Missing", ["InventoryCheck.Extra"] = "Extra",
            ["InventoryCheck.WrongLocation"] = "Wrong location", ["InventoryCheck.Damaged"] = "Damaged",
            ["InventoryCheck.Guidance.Missing"] = "Missing: recheck the area and movement history, then escalate if the item is not found.",
            ["InventoryCheck.Guidance.Extra"] = "Extra: identify the source, then create inbound or adjustment if valid.",
            ["InventoryCheck.Guidance.WrongLocation"] = "Wrong location: move the item back physically or post a move document to align the system.",
            ["InventoryCheck.Guidance.Damaged"] = "Damaged: quarantine the item, then create repair or status adjustment."
        },
        ["zh"] = new()
        {
            ["Document posted"] = "单据已过账",
            ["Inbound posted."] = "入库已过账。", ["Move posted."] = "移库已过账。",
            ["Repair send posted."] = "送修已过账。", ["Repair receive posted."] = "维修入库已过账。",
            ["Borrow lend posted."] = "借出单已过账。", ["Borrow return posted."] = "归还单已过账。",
            ["Adjustment posted."] = "调整已过账。", ["Inventory check posted."] = "盘点已过账。",
            ["Quantity inventory posted."] = "数量库存已过账。",
            ["Inventory check result"] = "盘点结果", ["Inventory check action required"] = "盘点需处理",
            ["Inventory check completed without discrepancy."] = "盘点完成，未发现差异。",
            ["Inventory check completed with discrepancies."] = "盘点发现差异。",
            ["InventoryCheck.Missing"] = "缺失", ["InventoryCheck.Extra"] = "多出",
            ["InventoryCheck.WrongLocation"] = "位置错误", ["InventoryCheck.Damaged"] = "损坏",
            ["InventoryCheck.Guidance.Missing"] = "缺失：复查区域和出入库历史，未找到时上报管理人员。",
            ["InventoryCheck.Guidance.Extra"] = "多出：确认来源，合法时创建入库或调整。",
            ["InventoryCheck.Guidance.WrongLocation"] = "位置错误：将实物放回正确位置，或创建移库单同步系统。",
            ["InventoryCheck.Guidance.Damaged"] = "损坏：隔离物料，创建维修单或状态调整。"
        }
    };

    protected sealed class OperationTransactionScope : IAsyncDisposable
    {
        private readonly IDbContextTransaction? _transaction;

        public OperationTransactionScope(IDbContextTransaction? transaction, bool ownsTransaction)
        {
            _transaction = transaction;
            OwnsTransaction = ownsTransaction;
        }

        public bool OwnsTransaction { get; }

        public Task CommitAsync(CancellationToken cancellationToken)
            => OwnsTransaction && _transaction != null
                ? _transaction.CommitAsync(cancellationToken)
                : Task.CompletedTask;

        public Task RollbackAsync(CancellationToken cancellationToken)
            => OwnsTransaction && _transaction != null
                ? _transaction.RollbackAsync(cancellationToken)
                : Task.CompletedTask;

        public ValueTask DisposeAsync()
            => OwnsTransaction && _transaction != null
                ? _transaction.DisposeAsync()
                : ValueTask.CompletedTask;
    }
}
