using ERP.Inventory.Domain.Common;
using ERP.Inventory.Domain.Enums;

namespace ERP.Inventory.Domain.Entities;

public class CurrentItemLocation : AuditableEntity
{
    public int ItemInstanceId { get; set; }
    public ItemInstance? ItemInstance { get; set; }
    public LocationType LocationType { get; set; }
    public int? WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public int? BinLocationId { get; set; }
    public BinLocation? BinLocation { get; set; }
    public int? ExternalPartyId { get; set; }
    public ExternalParty? ExternalParty { get; set; }
    public string? ExternalLocationText { get; set; }
    public string? ReferenceDocumentType { get; set; }
    public int? ReferenceDocumentId { get; set; }
    public string? ReferenceDocumentNo { get; set; }
    public DateTime UpdatedLocationAt { get; set; } = DateTime.UtcNow;
    public string UpdatedLocationBy { get; set; } = string.Empty;
}

public class ItemMovementHistory
{
    public long Id { get; set; }
    public int ItemInstanceId { get; set; }
    public ItemInstance? ItemInstance { get; set; }
    public MovementActionType ActionType { get; set; }
    public LocationType? FromLocationType { get; set; }
    public int? FromLocationId { get; set; }
    public string? FromLocationDisplay { get; set; }
    public LocationType? ToLocationType { get; set; }
    public int? ToLocationId { get; set; }
    public string? ToLocationDisplay { get; set; }
    public ItemStatus OldStatus { get; set; }
    public ItemStatus NewStatus { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public int DocumentId { get; set; }
    public string DocumentNo { get; set; } = string.Empty;
    public string? Note { get; set; }
    public DateTime PerformedAt { get; set; } = DateTime.UtcNow;
    public string PerformedBy { get; set; } = string.Empty;
}
