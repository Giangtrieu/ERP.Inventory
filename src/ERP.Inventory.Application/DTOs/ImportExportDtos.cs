namespace ERP.Inventory.Application.DTOs;

public sealed class ImportBatchDto
{
    public int Id { get; init; }
    public string BatchNo { get; init; } = string.Empty;
    public string ImportType { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public int TotalRows { get; init; }
    public int BlockingErrorRows { get; init; }
}

public sealed class ImportValidationRowDto
{
    public int Id { get; init; }
    public int RowNumber { get; init; }
    public string? ColumnName { get; init; }
    public string Severity { get; init; } = string.Empty;
    public string? Message { get; init; }
    public string? SuggestedFix { get; init; }
    public bool IsValid { get; init; }
}

public sealed class ExportFilterDto
{
    public int? WarehouseId { get; init; }
    public int? CategoryId { get; init; }
    public string? Status { get; init; }
    public string? Keyword { get; init; }
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public int? ItemInstanceId { get; init; }
    public string? UserName { get; init; }
    public string? Action { get; init; }
    public string? EntityName { get; init; }
    public string? ReferenceNo { get; init; }
}
