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

public sealed class ChartPointDto
{
    public string Label { get; init; } = string.Empty;
    public string? Key { get; init; }
    public decimal Value { get; init; }
}
