using ERP.Inventory.Application.Common;
using ERP.Inventory.Application.DTOs;
using ERP.Inventory.Application.Interfaces;
using ERP.Inventory.Domain.Entities;
using ERP.Inventory.Domain.Enums;
using ERP.Inventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ERP.Inventory.Infrastructure.Services;

public sealed class ItemSoftDeleteService : IItemSoftDeleteService
{
    private readonly InventoryDbContext _db;
    private readonly IDateTimeProvider _clock;

    public ItemSoftDeleteService(InventoryDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<ServiceResult<IReadOnlyCollection<DeletedItemDto>>> GetDeletedItemsAsync(string? keyword, int? warehouseId, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        if (!user.IsSuper)
        {
            return ServiceResult<IReadOnlyCollection<DeletedItemDto>>.Fail(Text(user.LanguageCode, "Only SuperPass can restore deleted items."));
        }

        var query = _db.ItemInstances
            .AsNoTracking()
            .Where(x => x.IsDeleted);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var key = keyword.Trim();
            query = query.Where(x =>
                x.Item!.ItemCode.Contains(key) ||
                x.Item.DefaultName.Contains(key) ||
                (x.SerialNumber != null && x.SerialNumber.Contains(key)) ||
                (x.Barcode != null && x.Barcode.Contains(key)) ||
                (x.DeleteReason != null && x.DeleteReason.Contains(key)));
        }

        if (warehouseId.HasValue)
        {
            query = query.Where(x => _db.CurrentItemLocations.Any(l => l.ItemInstanceId == x.Id && l.WarehouseId == warehouseId.Value));
        }

        var rows = await query
            .OrderByDescending(x => x.DeletedAt)
            .Take(300)
            .Select(x => new DeletedItemDto
            {
                ItemInstanceId = x.Id,
                ItemCode = x.Item != null ? x.Item.ItemCode : string.Empty,
                ItemName = x.Item != null ? x.Item.DefaultName : string.Empty,
                SerialNumber = x.SerialNumber,
                Barcode = x.Barcode,
                Status = x.Status.ToString(),
                DeletedAt = x.DeletedAt,
                DeletedBy = x.DeletedByUserName ?? x.DeletedByUserCode,
                DeleteReason = x.DeleteReason,
                DeleteSourceDocumentType = x.DeleteSourceDocumentType,
                DeleteSourceDocumentId = x.DeleteSourceDocumentId,
                CanRestore = x.CanRestore,
                Warehouse = _db.CurrentItemLocations
                    .Where(l => l.ItemInstanceId == x.Id)
                    .Select(l => l.Warehouse != null ? l.Warehouse.WarehouseCode : null)
                    .FirstOrDefault(),
                BinLocation = _db.CurrentItemLocations
                    .Where(l => l.ItemInstanceId == x.Id)
                    .Select(l => l.BinLocation != null ? l.BinLocation.FullPath : l.ExternalLocationText)
                    .FirstOrDefault()
            })
            .ToArrayAsync(cancellationToken);

        return ServiceResult<IReadOnlyCollection<DeletedItemDto>>.Ok(rows);
    }

    public async Task<ServiceResult<DocumentMutationResultDto>> SoftDeleteWrongItemAsync(int itemInstanceId, string? reason, CurrentUserContext user, string? sourceDocumentType = null, int? sourceDocumentId = null, CancellationToken cancellationToken = default)
    {
        var item = await _db.ItemInstances
            .Include(x => x.Item)
            .FirstOrDefaultAsync(x => x.Id == itemInstanceId, cancellationToken);

        var lastMovement = await _db.ItemMovementHistories.Where(x => x.ItemInstanceId == itemInstanceId).OrderByDescending(x => x.Id).FirstOrDefaultAsync(cancellationToken);
        if (sourceDocumentType == null && lastMovement != null) sourceDocumentType = lastMovement?.DocumentType;
        if (sourceDocumentId == null && lastMovement != null) sourceDocumentId = lastMovement?.DocumentId;
        if (item == null)
        {
            return ServiceResult<DocumentMutationResultDto>.Fail("Item instance not found.");
        }

        if (item.IsDeleted)
        {
            return ServiceResult<DocumentMutationResultDto>.Ok(Result("SoftDelete", item, sourceDocumentType, sourceDocumentId), Text(user.LanguageCode, "Item is already deleted."));
        }

        var current = await _db.CurrentItemLocations
            .Include(x => x.BinLocation)
            .Include(x => x.Warehouse)
            .Where(x => x.ItemInstanceId == itemInstanceId)
            .ToArrayAsync(cancellationToken);

        var warehouseId = current.Select(x => x.WarehouseId).FirstOrDefault(x => x.HasValue);
        if (warehouseId.HasValue && !user.CanAccessWarehouse(warehouseId.Value))
        {
            return ServiceResult<DocumentMutationResultDto>.Fail("Permission denied for this warehouse.");
        }

        await using var tx = await BeginTransactionAsync(cancellationToken);
        var now = _clock.UtcNow;
        var beforeStatus = item.Status;
        var normalizedReason = string.IsNullOrWhiteSpace(reason) ? Text(user.LanguageCode, "Delete Wrong Item") : reason.Trim();

        item.IsDeleted = true;
        item.IsActive = false;
        item.DeletedAt = now;
        item.DeletedByUserId = ParseUserId(user.UserId);
        item.DeletedByUserCode = user.UserId;
        item.DeletedByUserName = user.UserName;
        item.DeleteReason = normalizedReason;
        item.DeleteSourceDocumentType = sourceDocumentType;
        item.DeleteSourceDocumentId = sourceDocumentId;
        item.CanRestore = true;
        item.UpdatedAt = now;
        item.UpdatedBy = user.UserName;

        foreach (var location in current)
        {
            location.IsDeleted = true;
            location.DeletedAt = now;
            location.DeleteReason = normalizedReason;
            location.UpdatedAt = now;
            location.UpdatedBy = user.UserName;
        }

        AddMovement(item, MovementActionType.SoftDeleted, beforeStatus, item.Status, user, now, normalizedReason, sourceDocumentType, sourceDocumentId, current.FirstOrDefault());
        AddAudit(user, "SoftDelete", nameof(ItemInstance), item.Id, item.SerialNumber ?? item.Barcode ?? item.Id.ToString(), normalizedReason);

        await _db.SaveChangesAsync(cancellationToken);
        if (tx != null) await tx.CommitAsync(cancellationToken);
        return ServiceResult<DocumentMutationResultDto>.Ok(Result("SoftDelete", item, sourceDocumentType, sourceDocumentId), Text(user.LanguageCode, "Item was deleted successfully."));
    }

    public async Task<ServiceResult<DocumentMutationResultDto>> RestoreDeletedItemAsync(int itemInstanceId, string? reason, CurrentUserContext user, string? sourceDocumentType = null, int? sourceDocumentId = null, CancellationToken cancellationToken = default)
    {
        if (!user.IsSuper)
        {
            return ServiceResult<DocumentMutationResultDto>.Fail(Text(user.LanguageCode, "Only SuperPass can restore deleted items."));
        }

        var item = await _db.ItemInstances.Include(x => x.Item).FirstOrDefaultAsync(x => x.Id == itemInstanceId, cancellationToken);
        var lastMovement = await _db.ItemMovementHistories.Where(x => x.ItemInstanceId == itemInstanceId).OrderByDescending(x => x.Id).FirstOrDefaultAsync(cancellationToken);
        if (sourceDocumentType == null && lastMovement != null) sourceDocumentType = lastMovement?.DocumentType;
        if (sourceDocumentId == null && lastMovement != null) sourceDocumentId = lastMovement?.DocumentId;
        if (item == null)
        {
            return ServiceResult<DocumentMutationResultDto>.Fail("Item instance not found.");
        }

        if (!item.IsDeleted)
        {
            return ServiceResult<DocumentMutationResultDto>.Ok(Result("Restore", item, sourceDocumentType, sourceDocumentId), Text(user.LanguageCode, "Item is not deleted."));
        }

        if (!item.CanRestore || item.Item == null || !item.Item.IsActive)
        {
            return ServiceResult<DocumentMutationResultDto>.Fail(Text(user.LanguageCode, "Cannot restore because original bin is no longer valid."));
        }

        if (await HasSerialOrBarcodeConflictAsync(item, cancellationToken))
        {
            return ServiceResult<DocumentMutationResultDto>.Fail(Text(user.LanguageCode, "Cannot restore because serial or barcode already exists."));
        }

        var current = await _db.CurrentItemLocations
            .Include(x => x.BinLocation)
            .Include(x => x.Warehouse)
            .Where(x => x.ItemInstanceId == itemInstanceId)
            .ToArrayAsync(cancellationToken);
        var primaryLocation = current.FirstOrDefault();
        var locationError = await ValidateRestoreLocationAsync(item.Id, primaryLocation, cancellationToken);
        if (locationError != null)
        {
            return ServiceResult<DocumentMutationResultDto>.Fail(Text(user.LanguageCode, locationError));
        }

        await using var tx = await BeginTransactionAsync(cancellationToken);
        var now = _clock.UtcNow;
        var normalizedReason = string.IsNullOrWhiteSpace(reason) ? Text(user.LanguageCode, "Restore Item") : reason.Trim();

        item.IsDeleted = false;
        item.IsActive = true;
        item.RestoredAt = now;
        item.RestoredByUserId = ParseUserId(user.UserId);
        item.RestoredByUserCode = user.UserId;
        item.RestoreReason = normalizedReason;
        item.UpdatedAt = now;
        item.UpdatedBy = user.UserName;

        foreach (var location in current)
        {
            location.IsDeleted = false;
            location.DeletedAt = null;
            location.DeleteReason = null;
            location.UpdatedAt = now;
            location.UpdatedBy = user.UserName;
        }

        AddMovement(item, MovementActionType.Restored, item.Status, item.Status, user, now, normalizedReason, sourceDocumentType, sourceDocumentId, primaryLocation);
        AddAudit(user, "Restore", nameof(ItemInstance), item.Id, item.SerialNumber ?? item.Barcode ?? item.Id.ToString(), normalizedReason);

        await _db.SaveChangesAsync(cancellationToken);
        if (tx != null) await tx.CommitAsync(cancellationToken);
        return ServiceResult<DocumentMutationResultDto>.Ok(Result("Restore", item), Text(user.LanguageCode, "Item was restored successfully."));
    }

    private async Task<bool> HasSerialOrBarcodeConflictAsync(ItemInstance item, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(item.SerialNumber) &&
            await _db.ItemInstances.AnyAsync(x =>
                x.Id != item.Id &&
                x.ItemId == item.ItemId &&
                x.SerialNumber == item.SerialNumber &&
                x.IsActive &&
                !x.IsDeleted, cancellationToken))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(item.Barcode) &&
               await _db.ItemInstances.AnyAsync(x =>
                   x.Id != item.Id &&
                   x.Barcode == item.Barcode &&
                   x.IsActive &&
                   !x.IsDeleted, cancellationToken);
    }

    private async Task<string?> ValidateRestoreLocationAsync(int itemInstanceId, CurrentItemLocation? location, CancellationToken cancellationToken)
    {
        if (location?.BinLocationId == null)
        {
            return null;
        }

        if (location.BinLocation == null || !location.BinLocation.IsActive)
        {
            return "Cannot restore because original bin is no longer valid.";
        }

        var occupied = await _db.CurrentItemLocations.AnyAsync(x =>
            x.ItemInstanceId != itemInstanceId &&
            x.BinLocationId == location.BinLocationId &&
            !x.IsDeleted &&
            x.ItemInstance != null &&
            x.ItemInstance.IsActive &&
            !x.ItemInstance.IsDeleted &&
            x.ItemInstance.Status != ItemStatus.Lost &&
            x.ItemInstance.Status != ItemStatus.Disposed, cancellationToken);

        return occupied ? "Cannot restore because original bin is no longer valid." : null;
    }

    private async Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction?> BeginTransactionAsync(CancellationToken cancellationToken)
    {
        if (_db.Database.CurrentTransaction != null)
        {
            return null;
        }

        return await _db.Database.BeginTransactionAsync(cancellationToken);
    }

    private static DocumentMutationResultDto Result(string action, ItemInstance item, string? sourceDocumentType = null, int? sourceDocumentId = null)
    {
        return new DocumentMutationResultDto
        {
            Action = action,
            DocumentType = sourceDocumentType ?? nameof(ItemInstance),
            DocumentId = sourceDocumentId ?? item.Id,
            DocumentNo = item.DocumentNo ?? item.SerialNumber ?? item.Barcode ?? item.Id.ToString(),
            ProcessedAt = DateTime.UtcNow
        };
    }

    private void AddMovement(ItemInstance item, MovementActionType action, ItemStatus oldStatus, ItemStatus newStatus, CurrentUserContext user, DateTime now, string reason, string? sourceDocumentType, int? sourceDocumentId, CurrentItemLocation? location)
    {
        _db.ItemMovementHistories.Add(new ItemMovementHistory
        {
            ItemInstanceId = item.Id,
            ActionType = action,
            OldStatus = oldStatus,
            NewStatus = newStatus,
            FromLocationType = location?.LocationType,
            FromLocationId = location?.BinLocationId ?? location?.ExternalPartyId ?? location?.WarehouseId,
            FromLocationDisplay = LocationDisplay(location),
            ToLocationType = location?.LocationType,
            ToLocationId = location?.BinLocationId ?? location?.ExternalPartyId ?? location?.WarehouseId,
            ToLocationDisplay = LocationDisplay(location),
            DocumentType = sourceDocumentType ?? nameof(ItemInstance),
            DocumentId = sourceDocumentId ?? item.Id,
            DocumentNo = item.DocumentNo ?? item.SerialNumber ?? item.Barcode ?? item.Id.ToString(),
            Note = reason,
            PerformedAt = now,
            PerformedBy = user.UserName
        });
    }

    private void AddAudit(CurrentUserContext user, string action, string entityName, int entityId, string referenceNo, string reason)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = user.UserId,
            UserName = user.UserName,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            ReferenceNo = referenceNo,
            Result = reason,
            CreatedAt = _clock.UtcNow
        });
    }

    private static int? ParseUserId(string? userId)
        => int.TryParse(userId, out var id) ? id : null;

    private static string LocationDisplay(CurrentItemLocation? location)
    {
        if (location == null) return string.Empty;
        if (location.BinLocation != null) return location.BinLocation.FullPath;
        if (!string.IsNullOrWhiteSpace(location.ExternalLocationText)) return location.ExternalLocationText;
        if (location.ExternalParty != null) return location.ExternalParty.Name;
        if (location.Warehouse != null) return location.Warehouse.Name;
        return location.LocationType.ToString();
    }

    private static string Text(string language, string key)
    {
        return language switch
        {
            "en" => En.TryGetValue(key, out var en) ? en : key,
            "zh" => Zh.TryGetValue(key, out var zh) ? zh : key,
            _ => Vi.TryGetValue(key, out var vi) ? vi : key
        };
    }

    private static readonly Dictionary<string, string> Vi = new()
    {
        ["Deleted Items"] = "Dữ liệu đã xóa",
        ["Restore Item"] = "Khôi phục mặt hàng",
        ["Delete Wrong Item"] = "Xóa mặt hàng sai",
        ["Item was deleted successfully."] = "Đã xóa mặt hàng thành công.",
        ["Item was restored successfully."] = "Đã khôi phục mặt hàng thành công.",
        ["Item is already deleted."] = "Mặt hàng đã được xóa trước đó.",
        ["Item is not deleted."] = "Mặt hàng chưa bị xóa.",
        ["Only SuperPass can restore deleted items."] = "Chỉ SuperPass được khôi phục dữ liệu đã xóa.",
        ["Cannot restore because serial or barcode already exists."] = "Không thể khôi phục vì Serial hoặc Barcode đã tồn tại.",
        ["Cannot restore because original bin is no longer valid."] = "Không thể khôi phục vì vị trí cũ không còn hợp lệ.",
    };

    private static readonly Dictionary<string, string> En = new()
    {
        ["Deleted Items"] = "Deleted Items",
        ["Restore Item"] = "Restore Item",
        ["Delete Wrong Item"] = "Delete Wrong Item",
        ["Item was deleted successfully."] = "Item was deleted successfully.",
        ["Item was restored successfully."] = "Item was restored successfully.",
        ["Item is already deleted."] = "Item is already deleted.",
        ["Item is not deleted."] = "Item is not deleted.",
        ["Only SuperPass can restore deleted items."] = "Only SuperPass can restore deleted items.",
        ["Cannot restore because serial or barcode already exists."] = "Cannot restore because serial or barcode already exists.",
        ["Cannot restore because original bin is no longer valid."] = "Cannot restore because original bin is no longer valid.",
    };

    private static readonly Dictionary<string, string> Zh = new()
    {
        ["Deleted Items"] = "已删除数据",
        ["Restore Item"] = "恢复物料",
        ["Delete Wrong Item"] = "删除错误物料",
        ["Item was deleted successfully."] = "物料已成功删除。",
        ["Item was restored successfully."] = "物料已成功恢复。",
        ["Item is already deleted."] = "该物料已被删除。",
        ["Item is not deleted."] = "该物料未被删除。",
        ["Only SuperPass can restore deleted items."] = "只有 SuperPass 可以恢复已删除的数据。",
        ["Cannot restore because serial or barcode already exists."] = "无法恢复，因为序列号或条码已存在。",
        ["Cannot restore because original bin is no longer valid."] = "无法恢复，因为原库位不再有效。",
    };
}
