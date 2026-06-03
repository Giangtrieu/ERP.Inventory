namespace ERP.Inventory.Application.DTOs;

public sealed class DashboardSummaryDto
{
    public int TotalItems { get; init; }
    public int InStock { get; init; }
    public int Repairing { get; init; }
    public int LentOut { get; init; }
    public int OverdueReturn { get; init; }
    public int DamagedOrLost { get; init; }
}

/// <summary>
/// Tóm tắt tồn kho theo nghiệp vụ QuantityOnly (nhập/xuất/điều chỉnh số lượng).
/// </summary>
public sealed class QuantitySummaryDto
{
    /// <summary>Tổng số lô/SN đang được theo dõi (QuantityOnly instances).</summary>
    public int TotalSnCount { get; init; }
    /// <summary>Tổng số lượng tồn kho (sum of all positive balances).</summary>
    public decimal TotalQuantity { get; init; }
    /// <summary>Số lô/SN có tồn kho > 0.</summary>
    public int ActiveSnCount { get; init; }
    /// <summary>Số owner khác nhau có hàng trong kho.</summary>
    public int OwnerCount { get; init; }
    /// <summary>Top chart: tổng qty theo owner.</summary>
    public IReadOnlyCollection<ChartPointDto> ByOwner { get; init; } = Array.Empty<ChartPointDto>();
    /// <summary>Top chart: tổng qty theo item.</summary>
    public IReadOnlyCollection<ChartPointDto> ByItem { get; init; } = Array.Empty<ChartPointDto>();
    public IReadOnlyCollection<ChartPointDto> QuantityByItemCode { get; init; } = Array.Empty<ChartPointDto>();
    public IReadOnlyCollection<ChartPointDto> QuantityByItemCategory { get; init; } = Array.Empty<ChartPointDto>();
}

public sealed class ChartPointDto
{
    public string Label { get; init; } = string.Empty;
    public string? Key { get; init; }
    public decimal Value { get; init; }
    public decimal Percentage { get; init; }
}

public sealed class WarehouseMapDto
{
    public int WarehouseId { get; init; }
    public string WarehouseCode { get; init; } = string.Empty;
    public string WarehouseName { get; init; } = string.Empty;
    public string ViewMode { get; init; } = "occupancy";
    public int RackCount { get; init; }
    public int ShelfCount { get; init; }
    public int BinCount { get; init; }
    public int OccupiedBinCount { get; init; }
    public int EmptyBinCount { get; init; }
    public IReadOnlyCollection<WarehouseMapLegendDto> Legend { get; init; } = Array.Empty<WarehouseMapLegendDto>();
    public IReadOnlyCollection<RackMapDto> Racks { get; init; } = Array.Empty<RackMapDto>();
}

public sealed class RackMapDto
{
    public int RackId { get; init; }
    public string RackCode { get; init; } = string.Empty;
    public string RackName { get; init; } = string.Empty;
    public IReadOnlyCollection<ShelfMapDto> Shelves { get; init; } = Array.Empty<ShelfMapDto>();
}

public sealed class ShelfMapDto
{
    public int ShelfId { get; init; }
    public string ShelfCode { get; init; } = string.Empty;
    public string ShelfName { get; init; } = string.Empty;
    public IReadOnlyCollection<WarehouseMapBinDto> Bins { get; init; } = Array.Empty<WarehouseMapBinDto>();
}

public sealed class WarehouseMapBinDto
{
    public int BinLocationId { get; init; }
    public string BinCode { get; init; } = string.Empty;
    public string FullPath { get; init; } = string.Empty;
    public bool IsOccupied { get; init; }
    public int ItemCount { get; init; }
    public IReadOnlyCollection<string> ItemCodes { get; init; } = Array.Empty<string>();
    public IReadOnlyCollection<string> SerialNumbers { get; init; } = Array.Empty<string>();
    public IReadOnlyCollection<string> Barcodes { get; init; } = Array.Empty<string>();
    public IReadOnlyCollection<string> MTs { get; init; } = Array.Empty<string>();
    public IReadOnlyCollection<WarehouseMapItemDto> Items { get; init; } = Array.Empty<WarehouseMapItemDto>();
    //public string? ItemCode { get; init; }
    //public string? ItemName { get; init; }
    //public string? SerialNumber { get; init; }
    //public string? CategoryCode { get; init; }
    //public string? Status { get; init; }
    public string Color { get; init; } = "#ffffff";
    public string TextColor { get; init; } = "#111827";
}

public sealed class WarehouseMapItemDto
{
    public string? ItemCode { get; init; }
    public string? ItemName { get; init; }
    public string? SerialNumber { get; init; }
    public string? CategoryCode { get; init; }
    public string? Status { get; init; }
    public string? Barcode { get; init; }
    public string? MT { get; init; }
    public decimal Quantity { get; set; } = 1;
    public string TrackingType { get; set; } = "Serial";
}

public sealed class WarehouseMapLegendDto
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string Color { get; init; } = "#ffffff";
}
