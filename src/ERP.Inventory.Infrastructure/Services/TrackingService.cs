using ERP.Inventory.Application.Common;
using ERP.Inventory.Application.DTOs;
using ERP.Inventory.Application.Interfaces;
using ERP.Inventory.Domain.Entities;
using ERP.Inventory.Domain.Enums;
using ERP.Inventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ERP.Inventory.Infrastructure.Services;

public sealed class TrackingService : ITrackingService
{
    private readonly InventoryDbContext _db;

    public TrackingService(InventoryDbContext db)
    {
        _db = db;
    }

    public async Task<ServiceResult<IReadOnlyCollection<TrackingSearchResultDto>>> SearchAsync(string keyword, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return ServiceResult<IReadOnlyCollection<TrackingSearchResultDto>>.Fail("Keyword is required.");
        }

        var normalized = keyword.Trim();

        var rows = await _db.CurrentItemLocations
            .AsNoTracking()
            .Include(x => x.ItemInstance)!.ThenInclude(x => x!.Item)!.ThenInclude(x => x!.Translations)
            .Include(x => x.Warehouse)
            .Include(x => x.BinLocation)
            .Include(x => x.ExternalParty)
            .Where(x =>
                x.ItemInstance != null &&
                x.ItemInstance.Item != null &&
                (x.ItemInstance.Item.ItemCode.Contains(normalized) ||
                 (x.ItemInstance.SerialNumber != null && x.ItemInstance.SerialNumber.Contains(normalized)))) //||
                 //(x.ItemInstance.Barcode != null && x.ItemInstance.Barcode.Contains(normalized)) ||
                 //x.ItemInstance.Item.DefaultName.Contains(normalized)))
            .Take(25)
            .ToListAsync(cancellationToken);

        var result = rows
            .Where(x => x.WarehouseId == null || user.CanAccessWarehouse(x.WarehouseId.Value))
            .Select(x =>
            {
                var status = x.ItemInstance!.Status;
                return new TrackingSearchResultDto
                {
                    ItemInstanceId = x.ItemInstanceId,
                    ItemCode = x.ItemInstance.Item!.ItemCode,
                    //ItemName = GetItemName(x.ItemInstance.Item, user.LanguageCode),
                    SerialNumber = x.ItemInstance.SerialNumber,
                    Barcode = x.ItemInstance.Barcode,
                    Status = status,
                    HolderName = x.ExternalParty?.Name ?? x.Warehouse?.Name ?? "Warehouse",
                    LocationPath = CurrentLocationDisplay(x),
                    ReferenceDocumentNo = x.ReferenceDocumentNo,
                    UpdatedAt = x.UpdatedLocationAt,
                    UpdatedBy = x.UpdatedLocationBy,
                    CanMove = status is ItemStatus.Normal or ItemStatus.Damaged or ItemStatus.Scrapped or ItemStatus.InStock,
                    CanSendRepair = status is ItemStatus.Normal or ItemStatus.InStock or ItemStatus.Damaged,
                    CanLend = status is ItemStatus.Normal or ItemStatus.InStock,
                    ReferenceDocumentId = x.ReferenceDocumentId,
                    ReferenceDocumentType = x.ReferenceDocumentType,
                };
            })
            .ToArray();

        return ServiceResult<IReadOnlyCollection<TrackingSearchResultDto>>.Ok(result);
    }

    public async Task<ServiceResult<PagedResult<MovementHistoryDto>>> GetHistoryAsync(int itemInstanceId, int page, int pageSize, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var currentLocation = await _db.CurrentItemLocations.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ItemInstanceId == itemInstanceId, cancellationToken);

        if (currentLocation?.WarehouseId != null && !user.CanAccessWarehouse(currentLocation.WarehouseId.Value))
        {
            return ServiceResult<PagedResult<MovementHistoryDto>>.Fail("Permission denied for this warehouse.");
        }

        var query = _db.ItemMovementHistories.AsNoTracking()
            .Where(x => x.ItemInstanceId == itemInstanceId)
            .OrderByDescending(x => x.PerformedAt);

        var total = await query.CountAsync(cancellationToken);
        var rows = await query.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new MovementHistoryDto
            {
                Id = x.Id,
                PerformedAt = x.PerformedAt,
                ActionType = x.ActionType,
                FromLocation = x.FromLocationDisplay ?? string.Empty,
                ToLocation = x.ToLocationDisplay ?? string.Empty,
                OldStatus = x.OldStatus,
                NewStatus = x.NewStatus,
                DocumentNo = x.DocumentNo,
                PerformedBy = x.PerformedBy
            })
            .ToArrayAsync(cancellationToken);

        return ServiceResult<PagedResult<MovementHistoryDto>>.Ok(new PagedResult<MovementHistoryDto>
        {
            Items = rows,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        });
    }

    public async Task<ServiceResult<PagedResult<InventoryListRowDto>>> GetInventoryListAsync(string? keyword, int? warehouseId, int? categoryId, string? status, int page, int pageSize, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        if (!(pageSize == 0)) pageSize = Math.Clamp(pageSize, 1, 500);

        if (warehouseId.HasValue && !user.CanAccessWarehouse(warehouseId.Value))
        {
            return ServiceResult<PagedResult<InventoryListRowDto>>.Fail("Permission denied for this warehouse.");
        }

        ItemStatus? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ItemStatus>(status, true, out var statusValue))
        {
            parsedStatus = statusValue;
        }

        var query = _db.CurrentItemLocations
            .AsNoTracking()
            .Include(x => x.ItemInstance)!.ThenInclude(x => x!.Item)!.ThenInclude(x => x!.Translations)
            .Include(x => x.Warehouse)
            .Include(x => x.BinLocation)
            .Include(x => x.ExternalParty)
            .AsQueryable();

        // ── Warehouse scope (in SQL) ────────────────────────────────────────────
        if (warehouseId.HasValue)
        {
            query = query.Where(x => x.WarehouseId == warehouseId.Value);
        }
        else if (!user.IsAdmin)
        {
            query = query.Where(x => x.WarehouseId == null || user.WarehouseIds.Contains(x.WarehouseId.Value));
        }

        if (categoryId.HasValue)
        {
            query = query.Where(x => x.ItemInstance != null && x.ItemInstance.Item != null && x.ItemInstance.Item.CategoryId == categoryId.Value);
        }

        // ── Status filter ──────────────────────────────────────────────────────
        if (parsedStatus.HasValue)
        {
            if (parsedStatus == ItemStatus.InStock)
                // "InStock" group filter: Normal + Damaged + Scrapped
                query = query.Where(x => x.ItemInstance != null &&
                    (x.ItemInstance.Status == ItemStatus.Normal ||
                     x.ItemInstance.Status == ItemStatus.InStock ||
                     x.ItemInstance.Status == ItemStatus.Damaged ||
                     x.ItemInstance.Status == ItemStatus.Scrapped));
            else
                query = query.Where(x => x.ItemInstance != null && x.ItemInstance.Status == parsedStatus.Value);
        }


        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var key = keyword.Trim();
            query = query.Where(x => x.ItemInstance != null && x.ItemInstance.Item != null &&
                (x.ItemInstance.Item.ItemCode.Contains(key) ||
                 x.ItemInstance.Item.DefaultName.Contains(key) ||
                 (x.ItemInstance.SerialNumber != null && x.ItemInstance.SerialNumber.Contains(key)) ||
                 (x.ItemInstance.Barcode != null && x.ItemInstance.Barcode.Contains(key))));
        }

        var total = await query.CountAsync(cancellationToken);
        if (pageSize == 0) pageSize = total == 0 ? 1 : total;
        var locations = await query.AsSplitQuery()
            .OrderBy(x => x.ItemInstance!.Item!.ItemCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var rows = locations
            .Select(x => new InventoryListRowDto
            {
                ItemInstanceId = x.ItemInstanceId,
                ItemCode = x.ItemInstance!.Item!.ItemCode,
                ItemName = GetItemName(x.ItemInstance.Item, user.LanguageCode),
                SerialNumber = x.ItemInstance.SerialNumber,
                Barcode = x.ItemInstance.Barcode,
                Status = x.ItemInstance.Status,
                CurrentLocation = CurrentLocationDisplay(x),
                Holder = x.ExternalParty != null ? x.ExternalParty.Name : (x.Warehouse != null ? x.Warehouse.Name : "Unknown"),
                LastUpdatedAt = x.UpdatedLocationAt
            })
            .ToArray();

        return ServiceResult<PagedResult<InventoryListRowDto>>.Ok(new PagedResult<InventoryListRowDto>
        {
            Items = rows,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        });
    }

    public async Task<ServiceResult<PagedResult<InventoryListRowDto>>> GetListInventoryAsync(string? keyword, int? warehouseId, int? itemId, string? status, int page, int pageSize, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);

        if (pageSize != 0)
            pageSize = Math.Clamp(pageSize, 1, 500);

        if (warehouseId.HasValue && !user.CanAccessWarehouse(warehouseId.Value))
        {
            return ServiceResult<PagedResult<InventoryListRowDto>>
                .Fail("Permission denied for this warehouse.");
        }

        ItemStatus? parsedStatus = null;

        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse<ItemStatus>(status, true, out var statusValue))
        {
            parsedStatus = statusValue;
        }

        var query = _db.CurrentItemLocations
            .AsNoTracking()
            .AsQueryable();

        // ================= FILTER =================

        if (warehouseId.HasValue)
        {
            query = query.Where(x => x.WarehouseId == warehouseId.Value);
        }

        if (itemId.HasValue)
        {
            query = query.Where(x => x.ItemInstance != null &&
                                     x.ItemInstance.ItemId == itemId.Value);
        }

        if (parsedStatus.HasValue)
        {
            if (parsedStatus == ItemStatus.InStock)
                // "InStock" group: all in-warehouse items (Normal + legacy InStock + Damaged + Scrapped)
                query = query.Where(x => x.ItemInstance != null &&
                    (x.ItemInstance.Status == ItemStatus.Normal ||
                     x.ItemInstance.Status == ItemStatus.InStock ||
                     x.ItemInstance.Status == ItemStatus.Damaged ||
                     x.ItemInstance.Status == ItemStatus.Scrapped));
            else if (parsedStatus == ItemStatus.Normal)
                // "Normal" filter: items in normal condition (Normal + legacy InStock)
                query = query.Where(x => x.ItemInstance != null &&
                    (x.ItemInstance.Status == ItemStatus.Normal ||
                     x.ItemInstance.Status == ItemStatus.InStock));
            else
                query = query.Where(x => x.ItemInstance != null && x.ItemInstance.Status == parsedStatus.Value);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var key = keyword.Trim();

            query = query.Where(x =>
                x.ItemInstance != null &&
                x.ItemInstance.Item != null &&
                (
                    x.ItemInstance.Item.ItemCode.Contains(key) ||
                    //x.ItemInstance.Item.DefaultName.Contains(key) ||
                    (x.ItemInstance.SerialNumber != null &&
                     x.ItemInstance.SerialNumber.Contains(key)) ||
                    (x.ItemInstance.OwnerName != null &&
                     x.ItemInstance.OwnerName.Contains(key)) ||
                    //(x.ItemInstance.Barcode != null &&
                    // x.ItemInstance.Barcode.Contains(key)) ||
                     (x.BinLocation != null && x.BinLocation.BinCode.Contains(key))
                ));
        }

        if (!user.IsAdmin)
        {
            query = query.Where(x =>x.WarehouseId == null || user.WarehouseIds.Contains(x.WarehouseId.Value));
        }

        var total = await query.CountAsync(cancellationToken);
        if (pageSize == 0)
            pageSize = total == 0 ? 1 : total;

        var rows = await query.OrderBy(x => x.BinLocation!.BinCode).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new InventoryListRowDto
            {
                //ItemInstanceId = x.ItemInstanceId,
                ItemCode = x.ItemInstance!.Item!.ItemCode,
                //ItemName =x.ItemInstance.Item.Translations.Where(t =>t.LanguageCode == user.LanguageCode &&
                //            t.FieldName == "DefaultName").Select(t => t.Value).FirstOrDefault()?? x.ItemInstance.Item.DefaultName,
                SerialNumber = x.ItemInstance.SerialNumber,
                //MT = x.ItemInstance.MT,
                OwnerName = x.ItemInstance!.OwnerName,
                Status = x.ItemInstance.Status,
                CurrentLocation = x.BinLocation != null? x.BinLocation.BinCode: null,
                Holder =x.ExternalParty != null? x.ExternalParty.Name: (x.Warehouse != null? x.Warehouse.Name: "Unknown"),
                //LastUpdatedAt = x.UpdatedLocationAt
            })
            .ToArrayAsync(cancellationToken);

        return ServiceResult<PagedResult<InventoryListRowDto>>
            .Ok(new PagedResult<InventoryListRowDto>
            {
                Items = rows,
                Page = page,
                PageSize = pageSize,
                TotalCount = total
            });
    }

    private static string GetItemName(Item item, string languageCode)
    {
        return item.Translations
            .FirstOrDefault(x => x.LanguageCode == languageCode && x.FieldName == "DefaultName")
            ?.Value ?? item.DefaultName;
    }

    private static string CurrentLocationDisplay(CurrentItemLocation location)
    {
        if (location.BinLocation != null)
        {
            return location.BinLocation.FullPath;
        }

        if (!string.IsNullOrWhiteSpace(location.ExternalLocationText))
        {
            return location.ExternalParty != null
                ? $"{location.ExternalParty.Name} - {location.ExternalLocationText}"
                : location.ExternalLocationText;
        }

        if (location.ExternalParty != null)
        {
            return location.ExternalParty.Name;
        }

        return location.Warehouse?.Name ?? "Unknown";
    }
}
