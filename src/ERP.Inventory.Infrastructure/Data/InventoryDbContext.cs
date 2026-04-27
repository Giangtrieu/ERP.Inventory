using ERP.Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ERP.Inventory.Infrastructure.Data;

public class InventoryDbContext : DbContext
{
    public InventoryDbContext(DbContextOptions<InventoryDbContext> options) : base(options)
    {
    }

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<WarehouseZone> WarehouseZones => Set<WarehouseZone>();
    public DbSet<Rack> Racks => Set<Rack>();
    public DbSet<Shelf> Shelves => Set<Shelf>();
    public DbSet<BinLocation> BinLocations => Set<BinLocation>();

    public DbSet<ItemCategory> ItemCategories => Set<ItemCategory>();
    public DbSet<ItemUnit> ItemUnits => Set<ItemUnit>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<ItemTranslation> ItemTranslations => Set<ItemTranslation>();
    public DbSet<ItemInstance> ItemInstances => Set<ItemInstance>();
    public DbSet<ItemSerialRelation> ItemSerialRelations => Set<ItemSerialRelation>();
    public DbSet<ExternalParty> ExternalParties => Set<ExternalParty>();

    public DbSet<CurrentItemLocation> CurrentItemLocations => Set<CurrentItemLocation>();
    public DbSet<ItemMovementHistory> ItemMovementHistories => Set<ItemMovementHistory>();
    public DbSet<StockBalance> StockBalances => Set<StockBalance>();
    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();

    public DbSet<InboundDocument> InboundDocuments => Set<InboundDocument>();
    public DbSet<InboundDocumentLine> InboundDocumentLines => Set<InboundDocumentLine>();
    public DbSet<MoveDocument> MoveDocuments => Set<MoveDocument>();
    public DbSet<MoveDocumentLine> MoveDocumentLines => Set<MoveDocumentLine>();
    public DbSet<RepairDocument> RepairDocuments => Set<RepairDocument>();
    public DbSet<RepairDocumentLine> RepairDocumentLines => Set<RepairDocumentLine>();
    public DbSet<BorrowDocument> BorrowDocuments => Set<BorrowDocument>();
    public DbSet<BorrowDocumentLine> BorrowDocumentLines => Set<BorrowDocumentLine>();
    public DbSet<AdjustmentDocument> AdjustmentDocuments => Set<AdjustmentDocument>();
    public DbSet<AdjustmentDocumentLine> AdjustmentDocumentLines => Set<AdjustmentDocumentLine>();
    public DbSet<InventoryCheckDocument> InventoryCheckDocuments => Set<InventoryCheckDocument>();
    public DbSet<InventoryCheckLine> InventoryCheckLines => Set<InventoryCheckLine>();

    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
    public DbSet<ImportBatchRow> ImportBatchRows => Set<ImportBatchRow>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Translation> Translations => Set<Translation>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<UserWarehousePermission> UserWarehousePermissions => Set<UserWarehousePermission>();
    public DbSet<SystemUser> SystemUsers => Set<SystemUser>();
    public DbSet<SystemRole> SystemRoles => Set<SystemRole>();
    public DbSet<SystemUserRole> SystemUserRoles => Set<SystemUserRole>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureWarehouse(modelBuilder);
        ConfigureMasterData(modelBuilder);
        ConfigureTracking(modelBuilder);
        ConfigureDocuments(modelBuilder);
        ConfigureSystem(modelBuilder);

        foreach (var relationship in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
        {
            relationship.DeleteBehavior = DeleteBehavior.Restrict;
        }
    }

    private static void ConfigureWarehouse(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Company>().HasIndex(x => x.Code).IsUnique();
        modelBuilder.Entity<Branch>().HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
        modelBuilder.Entity<Warehouse>().HasIndex(x => x.WarehouseCode).IsUnique();
        modelBuilder.Entity<WarehouseZone>().HasIndex(x => new { x.WarehouseId, x.ZoneCode }).IsUnique();
        modelBuilder.Entity<Rack>().HasIndex(x => new { x.WarehouseZoneId, x.RackCode }).IsUnique();
        modelBuilder.Entity<Shelf>().HasIndex(x => new { x.RackId, x.ShelfCode }).IsUnique();
        modelBuilder.Entity<BinLocation>().HasIndex(x => new { x.WarehouseId, x.BinCode }).IsUnique();
        modelBuilder.Entity<BinLocation>().HasIndex(x => x.FullPath);
    }

    private static void ConfigureMasterData(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ItemCategory>().HasIndex(x => x.CategoryCode).IsUnique();
        modelBuilder.Entity<ItemUnit>().HasIndex(x => x.UnitCode).IsUnique();
        modelBuilder.Entity<Item>().HasIndex(x => x.ItemCode).IsUnique();
        modelBuilder.Entity<ItemTranslation>().HasIndex(x => new { x.ItemId, x.FieldName, x.LanguageCode }).IsUnique();
        modelBuilder.Entity<ItemInstance>().HasIndex(x => x.SerialNumber).IsUnique().HasFilter("[SerialNumber] IS NOT NULL");
        modelBuilder.Entity<ItemInstance>().HasIndex(x => x.Barcode).IsUnique().HasFilter("[Barcode] IS NOT NULL");
        modelBuilder.Entity<ExternalParty>().HasIndex(x => new { x.PartyType, x.PartyCode }).IsUnique();
    }

    private static void ConfigureTracking(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CurrentItemLocation>().HasIndex(x => x.ItemInstanceId).IsUnique();
        modelBuilder.Entity<ItemMovementHistory>().HasIndex(x => x.ItemInstanceId);
        modelBuilder.Entity<ItemMovementHistory>().HasIndex(x => new { x.DocumentType, x.DocumentId });
        modelBuilder.Entity<StockBalance>().Property(x => x.Quantity).HasPrecision(18, 4);
        modelBuilder.Entity<StockBalance>().HasIndex(x => new { x.WarehouseId, x.ItemId, x.BinLocationId, x.Status }).IsUnique();
        modelBuilder.Entity<InventoryTransaction>().Property(x => x.QuantityDelta).HasPrecision(18, 4);
        modelBuilder.Entity<InventoryTransaction>().HasIndex(x => new { x.DocumentType, x.DocumentId });
    }

    private static void ConfigureDocuments(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InboundDocument>().HasIndex(x => x.DocumentNo).IsUnique();
        modelBuilder.Entity<MoveDocument>().HasIndex(x => x.DocumentNo).IsUnique();
        modelBuilder.Entity<RepairDocument>().HasIndex(x => x.DocumentNo).IsUnique();
        modelBuilder.Entity<BorrowDocument>().HasIndex(x => x.DocumentNo).IsUnique();
        modelBuilder.Entity<AdjustmentDocument>().HasIndex(x => x.DocumentNo).IsUnique();
        modelBuilder.Entity<InventoryCheckDocument>().HasIndex(x => x.DocumentNo).IsUnique();

        modelBuilder.Entity<InboundDocumentLine>().Property(x => x.Quantity).HasPrecision(18, 4);
    }

    private static void ConfigureSystem(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SystemUser>().HasKey(x => x.Id);
        modelBuilder.Entity<SystemUser>().HasIndex(x => x.NormalizedUserName).IsUnique();
        modelBuilder.Entity<SystemRole>().HasIndex(x => x.NormalizedName).IsUnique();
        modelBuilder.Entity<SystemUserRole>().HasKey(x => new { x.UserId, x.RoleId });
        modelBuilder.Entity<SystemUserRole>()
            .HasOne(x => x.User)
            .WithMany(x => x.UserRoles)
            .HasForeignKey(x => x.UserId);
        modelBuilder.Entity<SystemUserRole>()
            .HasOne(x => x.Role)
            .WithMany(x => x.UserRoles)
            .HasForeignKey(x => x.RoleId);
        modelBuilder.Entity<ImportBatch>().HasIndex(x => x.BatchNo).IsUnique();
        modelBuilder.Entity<AuditLog>().HasIndex(x => new { x.EntityName, x.EntityId });
        modelBuilder.Entity<Translation>().HasIndex(x => new { x.EntityName, x.EntityId, x.FieldName, x.LanguageCode }).IsUnique();
        modelBuilder.Entity<UserWarehousePermission>().HasIndex(x => new { x.UserId, x.WarehouseId }).IsUnique();
    }
}
