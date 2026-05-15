using ERP.Inventory.Domain.Entities;
using ERP.Inventory.Domain.Enums;
using ERP.Inventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ERP.Inventory.Infrastructure.Seed;

public static class InventorySeedData
{
    public static async Task SeedAsync(InventoryDbContext db, CancellationToken cancellationToken = default)
    {
        //await db.Database.MigrateAsync(cancellationToken);

        //if (await db.Items.AnyAsync(cancellationToken))
        //{
        //    return;
        //}

        //var user = "default system";
        //var company = new Company { Code = "FOXCON", Name = "FOXCON", CreatedBy = user };
        //var branch = new Branch { Company = company, Code = "FII", Name = "FUYU", CreatedBy = user };
        //var warehouse = new Warehouse { Branch = branch, WarehouseCode = "B34", Name = "B34", CreatedBy = user };
        //var zone = new WarehouseZone { Warehouse = warehouse, ZoneCode = "F16", Name = "F16", CreatedBy = user };
        //var rack = new Rack { WarehouseZone = zone, RackCode = "R01", Name = "Rack 01", CreatedBy = user };
        //var shelf = new Shelf { Rack = rack, ShelfCode = "S01", Name = "Shelf 01", CreatedBy = user };

        //var bin01 = NewBin(warehouse, shelf, "B34_R01_S01", user);
        //var bin02 = NewBin(warehouse, shelf, "B34_R01_S02", user);
        //var bin03 = NewBin(warehouse, shelf, "B34_R01_S03", user);
        //var bin04 = NewBin(warehouse, shelf, "B34_R01_S04", user);
        //var bin05 = NewBin(warehouse, shelf, "B34_R01_S05", user);

        //var itemCategory = new ItemCategory { CategoryCode = "NVIDIA", Name = "NVIDIA", CreatedBy = user };
        ////var monitorCategory = new ItemCategory { CategoryCode = "MONITOR", Name = "Monitor", CreatedBy = user };
        //var unit = new ItemUnit { UnitCode = "PCS", Name = "Piece", CreatedBy = user };

        //var item1 = new Item { ItemCode = "SA019401", DefaultName = "SA019401", Category = itemCategory, Unit = unit, IsSerialManaged = true, CreatedBy = user };
        //item1.Translations.Add(new ItemTranslation { Item = item1, LanguageCode = "vi", FieldName = "DefaultName", Value = "SA019401", CreatedBy = user });
        //item1.Translations.Add(new ItemTranslation { Item = item1, LanguageCode = "zh", FieldName = "DefaultName", Value = "SA019401", CreatedBy = user });

        //var hp840 = new Item { ItemCode = "LAP-HP-840G8", DefaultName = "HP EliteBook 840 G8", Category = itemCategory, Unit = unit, IsSerialManaged = true, CreatedBy = user };
        //hp840.Translations.Add(new ItemTranslation { Item = hp840, LanguageCode = "vi", FieldName = "DefaultName", Value = "Laptop HP EliteBook 840 G8", CreatedBy = user });
        //hp840.Translations.Add(new ItemTranslation { Item = hp840, LanguageCode = "zh", FieldName = "DefaultName", Value = "HP EliteBook 840 G8", CreatedBy = user });

        //var monitor = new Item { ItemCode = "MON-DELL-P2422H", DefaultName = "Dell P2422H Monitor", Category = itemCategory, Unit = unit, IsSerialManaged = true, CreatedBy = user };
        //monitor.Translations.Add(new ItemTranslation { Item = monitor, LanguageCode = "vi", FieldName = "DefaultName", Value = "Man hinh Dell P2422H", CreatedBy = user });
        //monitor.Translations.Add(new ItemTranslation { Item = monitor, LanguageCode = "zh", FieldName = "DefaultName", Value = "Dell P2422H", CreatedBy = user });

        //var instance = new ItemInstance { Item = item1, SerialNumber = "6056066260002", Barcode = "6056066260002", Status = ItemStatus.InStock, CreatedBy = user };

        //db.AddRange(company, branch, warehouse, zone, rack, shelf, bin01, bin02, bin03, bin04, bin05, itemCategory, unit, item1, instance);
        //await db.SaveChangesAsync(cancellationToken);

        //db.CurrentItemLocations.Add(new CurrentItemLocation
        //{
        //    ItemInstanceId = instance.Id,
        //    LocationType = LocationType.BinLocation,
        //    WarehouseId = warehouse.Id,
        //    BinLocationId = bin05.Id,
        //    ReferenceDocumentType = nameof(InboundDocument),
        //    ReferenceDocumentNo = "INB-2026-000001",
        //    UpdatedLocationAt = DateTime.UtcNow,
        //    UpdatedLocationBy = user,
        //    CreatedBy = user
        //});

        //db.StockBalances.Add(new StockBalance
        //{
        //    WarehouseId = warehouse.Id,
        //    BinLocationId = bin05.Id,
        //    ItemId = item1.Id,
        //    Status = ItemStatus.InStock,
        //    Quantity = 1,
        //    CreatedBy = user
        //});

        //db.ItemMovementHistories.Add(new ItemMovementHistory
        //{
        //    ItemInstanceId = instance.Id,
        //    ActionType = MovementActionType.ImportOpening,
        //    FromLocationDisplay = "Excel opening",
        //    ToLocationType = LocationType.BinLocation,
        //    ToLocationId = bin05.Id,
        //    ToLocationDisplay = bin05.FullPath,
        //    OldStatus = ItemStatus.Reserved,
        //    NewStatus = ItemStatus.InStock,
        //    DocumentType = "Default System",
        //    DocumentId = 0,
        //    DocumentNo = "DS-000001",
        //    PerformedAt = DateTime.UtcNow,
        //    PerformedBy = user
        //});

        //db.ExternalParties.AddRange(
        //    new ExternalParty { PartyCode = "NVIDIA", Name = "NVIDIA", PartyType = ExternalPartyType.Supplier, CreatedBy = user },
        //    new ExternalParty { PartyCode = "RE", Name = "Repair Enginear", PartyType = ExternalPartyType.RepairVendor, CreatedBy = user },
        //    new ExternalParty { PartyCode = "IT", Name = "IT Department", PartyType = ExternalPartyType.Department, CreatedBy = user },
        //    new ExternalParty { PartyCode = "V3250286", Name = "Trần Thị Hướng", PartyType = ExternalPartyType.Borrower, CreatedBy = user });

        //await db.SaveChangesAsync(cancellationToken);
    }

    private static BinLocation NewBin(Warehouse warehouse, Shelf shelf, string code, string user)
    {
        return new BinLocation
        {
            Shelf = shelf,
            Warehouse = warehouse,
            BinCode = code,
            FullPath = $"{warehouse.WarehouseCode} /  F16 / R01 / S01 / {code}",
            CreatedBy = user
        };
    }
}
