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
