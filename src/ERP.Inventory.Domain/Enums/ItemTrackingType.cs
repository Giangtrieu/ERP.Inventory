namespace ERP.Inventory.Domain.Enums;

public enum ItemTrackingType
{
    /// <summary>Theo dõi từng đơn vị vật lý: vị trí, mượn/trả, sửa chữa...</summary>
    LocationTracked = 1,

    /// <summary>Chỉ quản lý theo số lượng, không theo dõi bin/instance riêng lẻ.</summary>
    QuantityOnly = 2
}
