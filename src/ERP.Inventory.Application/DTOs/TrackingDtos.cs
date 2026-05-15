using ERP.Inventory.Domain.Enums;

namespace ERP.Inventory.Application.DTOs;

public sealed class TrackingSearchResultDto
{
    public int ItemInstanceId { get; init; }
    public string ItemCode { get; init; } = string.Empty;
    public string ItemName { get; init; } = string.Empty;
    public string? SerialNumber { get; init; }
    public string? Barcode { get; init; }
    public ItemStatus Status { get; init; }
    public string HolderName { get; init; } = string.Empty;
    public string LocationPath { get; init; } = string.Empty;
    public string? ReferenceDocumentNo { get; init; }
    public DateTime UpdatedAt { get; init; }
    public string UpdatedBy { get; init; } = string.Empty;
    public bool CanMove { get; init; }
    public bool CanSendRepair { get; init; }
    public bool CanLend { get; init; }
    public string? ReferenceDocumentType { get; init; }
    public int? ReferenceDocumentId { get; init; }
}

public sealed class MovementHistoryDto
{
    public long Id { get; init; }
    public DateTime PerformedAt { get; init; }
    public MovementActionType ActionType { get; init; }
    public string FromLocation { get; init; } = string.Empty;
    public string ToLocation { get; init; } = string.Empty;
    public ItemStatus OldStatus { get; init; }
    public ItemStatus NewStatus { get; init; }
    public string DocumentNo { get; init; } = string.Empty;
    public string PerformedBy { get; init; } = string.Empty;
}

public sealed class InventoryListRowDto
{
    public int ItemInstanceId { get; init; }
    public string ItemCode { get; init; } = string.Empty;
    public string ItemName { get; init; } = string.Empty;
    public string? SerialNumber { get; init; }
    public string? MT { get; init; }
    public string? Barcode { get; init; }
    public ItemStatus Status { get; init; }
    public string CurrentLocation { get; init; } = string.Empty;
    public string Holder { get; init; } = string.Empty;
    public DateTime LastUpdatedAt { get; init; }
}

