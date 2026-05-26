using ERP.Inventory.Domain.Common;
using ERP.Inventory.Domain.Enums;

namespace ERP.Inventory.Domain.Entities;

public class ItemCategory : AuditableEntity
{
    public string CategoryCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public ICollection<Item> Items { get; set; } = new List<Item>();
}

public class ItemUnit : AuditableEntity
{
    public string UnitCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public ICollection<Item> Items { get; set; } = new List<Item>();
}

public class Item : AuditableEntity
{
    public string ItemCode { get; set; } = string.Empty;
    public string DefaultName { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public ItemCategory? Category { get; set; }
    public int UnitId { get; set; }
    public ItemUnit? Unit { get; set; }
    public bool IsSerialManaged { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<ItemTranslation> Translations { get; set; } = new List<ItemTranslation>();
    public ICollection<ItemInstance> Instances { get; set; } = new List<ItemInstance>();
}

public class ItemTranslation : AuditableEntity
{
    public int ItemId { get; set; }
    public Item? Item { get; set; }
    public string LanguageCode { get; set; } = "vi";
    public string FieldName { get; set; } = "DefaultName";
    public string Value { get; set; } = string.Empty;
}

public class ItemInstance : AuditableEntity
{
    public int ItemId { get; set; }
    public Item? Item { get; set; }
    public string? SerialNumber { get; set; }
    public string? DocumentNo { get; set; }
    public string? MT { get; set; }
    public string? Barcode { get; set; }
    public ItemStatus Status { get; set; } = ItemStatus.InStock;
    /// <summary>
    /// Phân biệt loại tracking: LocationTracked (serial/bin) hoặc QuantityOnly.
    /// Default = LocationTracked để backward compatible với dữ liệu cũ.
    /// </summary>
    public ItemTrackingType TrackingType { get; set; } = ItemTrackingType.LocationTracked;
    /// <summary>
    /// Chủ sở hữu hàng hóa (tên công ty, phòng ban, hoặc cá nhân).
    /// Nullable — không bắt buộc. Null = chưa gán owner.
    /// </summary>
    public string? OwnerName { get; set; }
    public bool IsActive { get; set; } = true;
}


public class ItemSerialRelation : AuditableEntity
{
    public int OldItemInstanceId { get; set; }
    public ItemInstance? OldItemInstance { get; set; }
    public int NewItemInstanceId { get; set; }
    public ItemInstance? NewItemInstance { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public int DocumentId { get; set; }
}

public class ExternalParty : AuditableEntity
{
    public string PartyCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ExternalPartyType PartyType { get; set; }
    public string? ContactName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public bool IsActive { get; set; } = true;
}

