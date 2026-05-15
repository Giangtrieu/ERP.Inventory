using ERP.Inventory.Application.Common;
using ERP.Inventory.Application.DTOs;
using ERP.Inventory.Application.Interfaces;
using ERP.Inventory.Domain.Entities;
using ERP.Inventory.Domain.Enums;
using ERP.Inventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ERP.Inventory.Infrastructure.Services;

public sealed class DashboardService : IDashboardService
{
    private readonly InventoryDbContext _db;

    public DashboardService(InventoryDbContext db)
    {
        _db = db;
    }

    public async Task<DashboardSummaryDto> GetSummaryAsync(int? warehouseId, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        var query = ApplyCurrentLocationWarehouseScope(
            _db.CurrentItemLocations.AsNoTracking().Include(x => x.ItemInstance).Where(x => x.ItemInstance != null),
            warehouseId, user);

        // Batch all status counts into a single query using GroupBy
        var statusCounts = await query
            .GroupBy(x => x.ItemInstance!.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToArrayAsync(cancellationToken);

        var total = statusCounts.Sum(x => x.Count);
        var inStock = statusCounts.Where(x => x.Status == ItemStatus.Normal || x.Status == ItemStatus.InStock || x.Status == ItemStatus.Scrapped || x.Status == ItemStatus.Damaged).Sum(x => x.Count);
        var repairing = statusCounts.Where(x => x.Status == ItemStatus.Repairing).Sum(x => x.Count);
        var lentOut = statusCounts.Where(x => x.Status == ItemStatus.LentOut).Sum(x => x.Count);
        var damagedOrLost = statusCounts
            .Where(x => x.Status == ItemStatus.Damaged || x.Status == ItemStatus.Lost || x.Status == ItemStatus.Scrapped)
            .Sum(x => x.Count);

        // Overdue borrow count
        var now = DateTime.UtcNow;
        var overdueQuery = _db.BorrowDocumentLines.AsNoTracking()
            .Include(x => x.BorrowDocument)
            .Include(x => x.FromBinLocation)
            .Include(x => x.TargetBinLocation)
            .Where(x => !x.IsReturned && x.BorrowDocument != null && x.BorrowDocument.DueDate < now);

        overdueQuery = ApplyBorrowWarehouseScope(overdueQuery, warehouseId, user);
        var overdue = await overdueQuery.CountAsync(cancellationToken);

        return new DashboardSummaryDto
        {
            TotalItems = total,
            InStock = inStock,
            Repairing = repairing,
            LentOut = lentOut,
            OverdueReturn = overdue,
            DamagedOrLost = damagedOrLost
        };
    }

    public async Task<IReadOnlyCollection<ChartPointDto>> GetStockByWarehouseAsync(string? status, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        var query = _db.CurrentItemLocations.AsNoTracking()
            .Include(x => x.Warehouse)
            .Include(x => x.ItemInstance)
            .Where(x => x.ItemInstance != null);

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ItemStatus>(status, true, out var parsedStatus))
        {
            if (parsedStatus == ItemStatus.InStock)
            {
                query = query.Where(x =>
                x.ItemInstance != null &&
                (
                    x.ItemInstance.Status == ItemStatus.InStock
                    || x.ItemInstance.Status == ItemStatus.Normal
                    || x.ItemInstance.Status == ItemStatus.Damaged
                    || x.ItemInstance.Status == ItemStatus.Scrapped
                ));
            }
            else query = query.Where(x => x.ItemInstance!.Status == parsedStatus);
        }

        if (!user.IsAdmin)
        {
            query = query.Where(x => x.WarehouseId == null || user.WarehouseIds.Contains(x.WarehouseId.Value));
        }

        return await query.GroupBy(x => x.Warehouse != null ? x.Warehouse.Name : "Unknown")
            .Select(x => new ChartPointDto { Label = x.Key, Value = x.Count() })
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<ChartPointDto>> GetStockByStatusAsync(int? warehouseId, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        var query = ApplyCurrentLocationWarehouseScope(
            _db.CurrentItemLocations.AsNoTracking().Include(x => x.ItemInstance).Where(x => x.ItemInstance != null),
            warehouseId, user);

        return await query.GroupBy(x => x.ItemInstance!.Status)
            .Select(x => new ChartPointDto { Key = x.Key.ToString(), Label = x.Key.ToString(), Value = x.Count() })
            .OrderByDescending(x => x.Value)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<ChartPointDto>> GetMovementTrendAsync(int? warehouseId, int days, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        days = Math.Clamp(days, 7, 90);
        var fromDate = DateTime.UtcNow.Date.AddDays(-days + 1);
        var query = _db.ItemMovementHistories.AsNoTracking()
            .Where(x => x.PerformedAt >= fromDate);

        query = ApplyMovementWarehouseScope(query, warehouseId, user);

        var rows = await query
            .GroupBy(x => x.PerformedAt.Date)
            .Select(x => new { Date = x.Key, Count = x.Count() })
            .OrderBy(x => x.Date)
            .ToArrayAsync(cancellationToken);

        var byDate = rows.ToDictionary(x => x.Date.Date, x => x.Count);
        return Enumerable.Range(0, days)
            .Select(i => fromDate.AddDays(i))
            .Select(x => new ChartPointDto
            {
                Key = x.ToString("yyyy-MM-dd"),
                Label = x.ToString("dd/MM"),
                Value = byDate.TryGetValue(x.Date, out var count) ? count : 0
            })
            .ToArray();
    }

    public async Task<IReadOnlyCollection<ChartPointDto>> GetMovementByActionAsync(int? warehouseId, int days, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        days = Math.Clamp(days, 7, 90);
        var fromDate = DateTime.UtcNow.Date.AddDays(-days + 1);
        var query = _db.ItemMovementHistories.AsNoTracking()
            .Where(x => x.PerformedAt >= fromDate);

        query = ApplyMovementWarehouseScope(query, warehouseId, user);

        return await query.GroupBy(x => x.ActionType)
            .Select(x => new ChartPointDto { Key = x.Key.ToString(), Label = x.Key.ToString(), Value = x.Count() })
            .OrderByDescending(x => x.Value)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<ChartPointDto>> GetStockByCategoryAsync(int? warehouseId, string? status, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        var query = ApplyCurrentLocationWarehouseScope(
            _db.CurrentItemLocations.AsNoTracking()
                .Include(x => x.ItemInstance)!.ThenInclude(x => x!.Item)!.ThenInclude(x => x!.Category)
                .Where(x => x.ItemInstance != null && x.ItemInstance.Item != null),
            warehouseId, user);

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ItemStatus>(status, true, out var parsedStatus))
        {;
            if (parsedStatus == ItemStatus.InStock)
            {
                query = query.Where(x =>
                x.ItemInstance != null &&
                (
                    x.ItemInstance.Status == ItemStatus.InStock
                    || x.ItemInstance.Status == ItemStatus.Normal
                    || x.ItemInstance.Status == ItemStatus.Damaged
                    || x.ItemInstance.Status == ItemStatus.Scrapped
                ));
            }
            else query = query.Where(x => x.ItemInstance!.Status == parsedStatus);
        }

        return await query
            .GroupBy(x => x.ItemInstance!.Item!.Category != null ? x.ItemInstance.Item.Category.CategoryCode : "Unknown")
            .Select(x => new ChartPointDto { Key = x.Key, Label = x.Key, Value = x.Count() })
            .OrderByDescending(x => x.Value)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<ChartPointDto>> GetLocationUtilizationAsync(int? warehouseId, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        var bins = _db.BinLocations.AsNoTracking().Where(x => x.IsActive);
        if (warehouseId.HasValue)
        {
            bins = user.CanAccessWarehouse(warehouseId.Value)
                ? bins.Where(x => x.WarehouseId == warehouseId.Value)
                : bins.Where(x => false);
        }
        else if (!user.IsAdmin)
        {
            bins = bins.Where(x => user.WarehouseIds.Contains(x.WarehouseId));
        }

        // Use a single query with left join instead of N+1 subquery
        var total = await bins.CountAsync(cancellationToken);
        var occupied = await (
            from bin in bins
            where _db.CurrentItemLocations.Any(c =>
                c.BinLocationId == bin.Id &&
                c.ItemInstance != null &&
                c.ItemInstance.IsActive &&
                c.ItemInstance.Status != ItemStatus.Lost &&
                c.ItemInstance.Status != ItemStatus.Disposed)
            select bin
        ).CountAsync(cancellationToken);
        var empty = Math.Max(0, total - occupied);

        return new[]
        {
            new ChartPointDto { Key = "OccupiedBins", Label = "Occupied bins", Value = occupied },
            new ChartPointDto { Key = "EmptyBins", Label = "Empty bins", Value = empty }
        };
    }

    public async Task<IReadOnlyCollection<ChartPointDto>> GetOverdueBorrowAgingAsync(int? warehouseId, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var query = _db.BorrowDocumentLines.AsNoTracking()
            .Include(x => x.BorrowDocument)
            .Include(x => x.FromBinLocation)
            .Include(x => x.TargetBinLocation)
            .Where(x => !x.IsReturned && x.BorrowDocument != null && x.BorrowDocument.DueDate < now);

        query = ApplyBorrowWarehouseScope(query, warehouseId, user);

        var rows = await query.Select(x => x.BorrowDocument!.DueDate).ToArrayAsync(cancellationToken);
        var oneToSeven = rows.Count(x => (now.Date - x.Date).TotalDays <= 7);
        var eightToThirty = rows.Count(x => (now.Date - x.Date).TotalDays > 7 && (now.Date - x.Date).TotalDays <= 30);
        var overThirty = rows.Count(x => (now.Date - x.Date).TotalDays > 30);

        return new[]
        {
            new ChartPointDto { Key = "1-7", Label = "1-7 days", Value = oneToSeven },
            new ChartPointDto { Key = "8-30", Label = "8-30 days", Value = eightToThirty },
            new ChartPointDto { Key = "30+", Label = "Over 30 days", Value = overThirty }
        };
    }

    public async Task<QuantitySummaryDto> GetQuantitySummaryAsync(int? warehouseId, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        // Scope balance query by warehouse + user permissions
        var query = _db.QuantityStockBalances.AsNoTracking()
            .Include(x => x.Item)
            .Where(x => x.Quantity > 0);

        if (warehouseId.HasValue)
        {
            query = user.CanAccessWarehouse(warehouseId.Value)
                ? query.Where(x => x.WarehouseId == warehouseId.Value)
                : query.Where(x => false);
        }
        else if (!user.IsAdmin)
        {
            query = query.Where(x => user.WarehouseIds.Contains(x.WarehouseId));
        }

        // Aggregate in one query
        var rows = await query
            .Select(x => new
            {
                x.SnCode,
                x.Quantity,
                ItemCode = x.Item != null ? x.Item.ItemCode : string.Empty,
                // Join to ItemInstance to get OwnerName
                OwnerName = _db.ItemInstances
                    .Where(i => i.ItemId == x.ItemId &&
                                i.SerialNumber == x.SnCode &&
                                i.TrackingType == ItemTrackingType.QuantityOnly)
                    .Select(i => i.OwnerName)
                    .FirstOrDefault()
            })
            .ToArrayAsync(cancellationToken);

        if (!rows.Any())
        {
            return new QuantitySummaryDto();
        }

        var totalQty      = rows.Sum(x => x.Quantity);
        var activeSnCount = rows.Select(x => x.SnCode).Distinct().Count();
        var totalSnCount  = activeSnCount; // all rows here already have Quantity > 0
        var ownerCount    = rows.Where(x => !string.IsNullOrWhiteSpace(x.OwnerName))
                               .Select(x => x.OwnerName!)
                               .Distinct(StringComparer.OrdinalIgnoreCase)
                               .Count();

        // Chart: Qty by Owner (top 10)
        var byOwner = rows
            .GroupBy(x => string.IsNullOrWhiteSpace(x.OwnerName) ? "(No Owner)" : x.OwnerName!,
                     StringComparer.OrdinalIgnoreCase)
            .Select(g => new ChartPointDto { Key = g.Key, Label = g.Key, Value = g.Sum(x => x.Quantity) })
            .OrderByDescending(x => x.Value)
            .Take(10)
            .ToArray();

        // Chart: Qty by Item (top 10)
        var byItem = rows
            .GroupBy(x => string.IsNullOrWhiteSpace(x.ItemCode) ? "Unknown" : x.ItemCode,
                     StringComparer.OrdinalIgnoreCase)
            .Select(g => new ChartPointDto { Key = g.Key, Label = g.Key, Value = g.Sum(x => x.Quantity) })
            .OrderByDescending(x => x.Value)
            .Take(10)
            .ToArray();

        return new QuantitySummaryDto
        {
            TotalSnCount  = totalSnCount,
            TotalQuantity = totalQty,
            ActiveSnCount = activeSnCount,
            OwnerCount    = ownerCount,
            ByOwner       = byOwner,
            ByItem        = byItem
        };
    }

    // ─── Shared scope helpers ────────────────────────────────

    private IQueryable<CurrentItemLocation> ApplyCurrentLocationWarehouseScope(IQueryable<CurrentItemLocation> query, int? warehouseId, CurrentUserContext user)
    {
        if (warehouseId.HasValue)
        {
            return user.CanAccessWarehouse(warehouseId.Value)
                ? query.Where(x => x.WarehouseId == warehouseId.Value)
                : query.Where(x => false);
        }

        if (!user.IsAdmin)
        {
            query = query.Where(x => x.WarehouseId == null || user.WarehouseIds.Contains(x.WarehouseId.Value));
        }

        return query;
    }

    private IQueryable<ItemMovementHistory> ApplyMovementWarehouseScope(IQueryable<ItemMovementHistory> query, int? warehouseId, CurrentUserContext user)
    {
        if (warehouseId.HasValue)
        {
            if (!user.CanAccessWarehouse(warehouseId.Value))
            {
                return query.Where(x => false);
            }

            return ApplyMovementWarehouseIds(query, new[] { warehouseId.Value });
        }

        if (!user.IsAdmin)
        {
            query = ApplyMovementWarehouseIds(query, user.WarehouseIds.ToArray());
        }

        return query;
    }

    private IQueryable<ItemMovementHistory> ApplyMovementWarehouseIds(IQueryable<ItemMovementHistory> query, IReadOnlyCollection<int> warehouseIds)
    {
        if (warehouseIds.Count == 0)
        {
            return query.Where(x => false);
        }

        // Simplified: use only CurrentItemLocations to scope warehouse access
        return query.Where(x =>
            _db.CurrentItemLocations.Any(c =>
                c.ItemInstanceId == x.ItemInstanceId &&
                c.WarehouseId.HasValue &&
                warehouseIds.Contains(c.WarehouseId.Value)));
    }

    private static IQueryable<BorrowDocumentLine> ApplyBorrowWarehouseScope(IQueryable<BorrowDocumentLine> query, int? warehouseId, CurrentUserContext user)
    {
        if (warehouseId.HasValue)
        {
            return user.CanAccessWarehouse(warehouseId.Value)
                ? query.Where(x =>
                    (x.TargetBinLocation != null && x.TargetBinLocation.WarehouseId == warehouseId.Value) ||
                    (x.TargetBinLocation == null && x.FromBinLocation != null && x.FromBinLocation.WarehouseId == warehouseId.Value))
                : query.Where(x => false);
        }

        if (!user.IsAdmin)
        {
            query = query.Where(x =>
                (x.TargetBinLocation != null && user.WarehouseIds.Contains(x.TargetBinLocation.WarehouseId)) ||
                (x.TargetBinLocation == null && x.FromBinLocation != null && user.WarehouseIds.Contains(x.FromBinLocation.WarehouseId)));
        }

        return query;
    }
}
