using ERP.Inventory.Domain.Entities;
using ERP.Inventory.Domain.Enums;
using ERP.Inventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ERP.Inventory.Infrastructure.Seed;

public static class InventorySeedData
{
    public static async Task SeedAsync(InventoryDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Database.MigrateAsync(cancellationToken);

        if (await db.Items.AnyAsync(cancellationToken))
        {
            return;
        }

        var user = "seed";
        var company = new Company { Code = "COMP", Name = "Demo Company", CreatedBy = user };
        var branch = new Branch { Company = company, Code = "HN", Name = "Ha Noi Branch", CreatedBy = user };
        var warehouse = new Warehouse { Branch = branch, WarehouseCode = "HN-WH-01", Name = "Kho Ha Noi", CreatedBy = user };
        var zone = new WarehouseZone { Warehouse = warehouse, ZoneCode = "A", Name = "Zone A", CreatedBy = user };
        var rack = new Rack { WarehouseZone = zone, RackCode = "R03", Name = "Rack 03", CreatedBy = user };
        var shelf = new Shelf { Rack = rack, ShelfCode = "S02", Name = "Shelf 02", CreatedBy = user };

        var bin01 = NewBin(warehouse, shelf, "B01", user);
        var bin02 = NewBin(warehouse, shelf, "B02", user);
        var bin03 = NewBin(warehouse, shelf, "B03", user);
        var bin04 = NewBin(warehouse, shelf, "B04", user);
        var bin05 = NewBin(warehouse, shelf, "B05", user);

        var laptopCategory = new ItemCategory { CategoryCode = "LAPTOP", Name = "Laptop", CreatedBy = user };
        var monitorCategory = new ItemCategory { CategoryCode = "MONITOR", Name = "Monitor", CreatedBy = user };
        var unit = new ItemUnit { UnitCode = "PCS", Name = "Piece", CreatedBy = user };

        var dell7420 = new Item { ItemCode = "LAP-DELL-7420", DefaultName = "Dell Latitude 7420", Category = laptopCategory, Unit = unit, IsSerialManaged = true, CreatedBy = user };
        dell7420.Translations.Add(new ItemTranslation { Item = dell7420, LanguageCode = "vi", FieldName = "DefaultName", Value = "Laptop Dell Latitude 7420", CreatedBy = user });
        dell7420.Translations.Add(new ItemTranslation { Item = dell7420, LanguageCode = "zh", FieldName = "DefaultName", Value = "Dell Latitude 7420", CreatedBy = user });

        var hp840 = new Item { ItemCode = "LAP-HP-840G8", DefaultName = "HP EliteBook 840 G8", Category = laptopCategory, Unit = unit, IsSerialManaged = true, CreatedBy = user };
        hp840.Translations.Add(new ItemTranslation { Item = hp840, LanguageCode = "vi", FieldName = "DefaultName", Value = "Laptop HP EliteBook 840 G8", CreatedBy = user });
        hp840.Translations.Add(new ItemTranslation { Item = hp840, LanguageCode = "zh", FieldName = "DefaultName", Value = "HP EliteBook 840 G8", CreatedBy = user });

        var monitor = new Item { ItemCode = "MON-DELL-P2422H", DefaultName = "Dell P2422H Monitor", Category = monitorCategory, Unit = unit, IsSerialManaged = true, CreatedBy = user };
        monitor.Translations.Add(new ItemTranslation { Item = monitor, LanguageCode = "vi", FieldName = "DefaultName", Value = "Man hinh Dell P2422H", CreatedBy = user });
        monitor.Translations.Add(new ItemTranslation { Item = monitor, LanguageCode = "zh", FieldName = "DefaultName", Value = "Dell P2422H", CreatedBy = user });

        var instance = new ItemInstance { Item = dell7420, SerialNumber = "SN-DELL-7420-00921", Barcode = "BC-8939912001", Status = ItemStatus.InStock, CreatedBy = user };

        db.AddRange(company, branch, warehouse, zone, rack, shelf, bin01, bin02, bin03, bin04, bin05, laptopCategory, monitorCategory, unit, dell7420, hp840, monitor, instance);
        await db.SaveChangesAsync(cancellationToken);

        db.CurrentItemLocations.Add(new CurrentItemLocation
        {
            ItemInstanceId = instance.Id,
            LocationType = LocationType.BinLocation,
            WarehouseId = warehouse.Id,
            BinLocationId = bin05.Id,
            ReferenceDocumentType = nameof(InboundDocument),
            ReferenceDocumentNo = "INB-2026-000001",
            UpdatedLocationAt = DateTime.Now,
            UpdatedLocationBy = user,
            CreatedBy = user
        });

        db.StockBalances.Add(new StockBalance
        {
            WarehouseId = warehouse.Id,
            BinLocationId = bin05.Id,
            ItemId = dell7420.Id,
            Status = ItemStatus.InStock,
            Quantity = 1,
            CreatedBy = user
        });

        db.ItemMovementHistories.Add(new ItemMovementHistory
        {
            ItemInstanceId = instance.Id,
            ActionType = MovementActionType.ImportOpening,
            FromLocationDisplay = "Excel opening",
            ToLocationType = LocationType.BinLocation,
            ToLocationId = bin05.Id,
            ToLocationDisplay = bin05.FullPath,
            OldStatus = ItemStatus.Reserved,
            NewStatus = ItemStatus.InStock,
            DocumentType = "Seed",
            DocumentId = 0,
            DocumentNo = "SEED-000001",
            PerformedAt = DateTime.Now,
            PerformedBy = user
        });

        db.ExternalParties.AddRange(
            new ExternalParty { PartyCode = "SUP-DEMO", Name = "Demo Supplier", PartyType = ExternalPartyType.Supplier, CreatedBy = user },
            new ExternalParty { PartyCode = "FPT-SERVICE", Name = "FPT Service Center", PartyType = ExternalPartyType.RepairVendor, CreatedBy = user },
            new ExternalParty { PartyCode = "IT-DEPT", Name = "IT Department", PartyType = ExternalPartyType.Department, CreatedBy = user },
            new ExternalParty { PartyCode = "EMP-001", Name = "Nguyen Van A", PartyType = ExternalPartyType.Borrower, CreatedBy = user });

        await db.SaveChangesAsync(cancellationToken);
    }

    private static BinLocation NewBin(Warehouse warehouse, Shelf shelf, string code, string user)
    {
        return new BinLocation
        {
            Shelf = shelf,
            Warehouse = warehouse,
            BinCode = code,
            FullPath = $"{warehouse.WarehouseCode} / Zone A / Rack 03 / Shelf 02 / Bin {code}",
            CreatedBy = user
        };
    }
}
