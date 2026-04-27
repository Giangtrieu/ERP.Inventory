CREATE TABLE Companies (
    Id int IDENTITY(1,1) PRIMARY KEY,
    Code nvarchar(50) NOT NULL UNIQUE,
    Name nvarchar(255) NOT NULL,
    IsActive bit NOT NULL DEFAULT 1,
    CreatedAt datetime2 NOT NULL DEFAULT sysutcdatetime(),
    CreatedBy nvarchar(100) NOT NULL,
    UpdatedAt datetime2 NULL,
    UpdatedBy nvarchar(100) NULL
);

CREATE TABLE Branches (
    Id int IDENTITY(1,1) PRIMARY KEY,
    CompanyId int NOT NULL REFERENCES Companies(Id),
    Code nvarchar(50) NOT NULL,
    Name nvarchar(255) NOT NULL,
    IsActive bit NOT NULL DEFAULT 1,
    CreatedAt datetime2 NOT NULL DEFAULT sysutcdatetime(),
    CreatedBy nvarchar(100) NOT NULL,
    UpdatedAt datetime2 NULL,
    UpdatedBy nvarchar(100) NULL,
    CONSTRAINT UQ_Branches_Company_Code UNIQUE (CompanyId, Code)
);

CREATE TABLE Warehouses (
    Id int IDENTITY(1,1) PRIMARY KEY,
    BranchId int NOT NULL REFERENCES Branches(Id),
    WarehouseCode nvarchar(50) NOT NULL UNIQUE,
    Name nvarchar(255) NOT NULL,
    IsActive bit NOT NULL DEFAULT 1,
    CreatedAt datetime2 NOT NULL DEFAULT sysutcdatetime(),
    CreatedBy nvarchar(100) NOT NULL,
    UpdatedAt datetime2 NULL,
    UpdatedBy nvarchar(100) NULL
);

CREATE TABLE WarehouseZones (
    Id int IDENTITY(1,1) PRIMARY KEY,
    WarehouseId int NOT NULL REFERENCES Warehouses(Id),
    ZoneCode nvarchar(50) NOT NULL,
    Name nvarchar(255) NOT NULL,
    IsActive bit NOT NULL DEFAULT 1,
    CreatedAt datetime2 NOT NULL DEFAULT sysutcdatetime(),
    CreatedBy nvarchar(100) NOT NULL,
    UpdatedAt datetime2 NULL,
    UpdatedBy nvarchar(100) NULL,
    CONSTRAINT UQ_WarehouseZones_Warehouse_Code UNIQUE (WarehouseId, ZoneCode)
);

CREATE TABLE Racks (
    Id int IDENTITY(1,1) PRIMARY KEY,
    WarehouseZoneId int NOT NULL REFERENCES WarehouseZones(Id),
    RackCode nvarchar(50) NOT NULL,
    Name nvarchar(255) NOT NULL,
    IsActive bit NOT NULL DEFAULT 1,
    CreatedAt datetime2 NOT NULL DEFAULT sysutcdatetime(),
    CreatedBy nvarchar(100) NOT NULL,
    UpdatedAt datetime2 NULL,
    UpdatedBy nvarchar(100) NULL,
    CONSTRAINT UQ_Racks_Zone_Code UNIQUE (WarehouseZoneId, RackCode)
);

CREATE TABLE Shelves (
    Id int IDENTITY(1,1) PRIMARY KEY,
    RackId int NOT NULL REFERENCES Racks(Id),
    ShelfCode nvarchar(50) NOT NULL,
    Name nvarchar(255) NOT NULL,
    IsActive bit NOT NULL DEFAULT 1,
    CreatedAt datetime2 NOT NULL DEFAULT sysutcdatetime(),
    CreatedBy nvarchar(100) NOT NULL,
    UpdatedAt datetime2 NULL,
    UpdatedBy nvarchar(100) NULL,
    CONSTRAINT UQ_Shelves_Rack_Code UNIQUE (RackId, ShelfCode)
);

CREATE TABLE BinLocations (
    Id int IDENTITY(1,1) PRIMARY KEY,
    ShelfId int NOT NULL REFERENCES Shelves(Id),
    WarehouseId int NOT NULL REFERENCES Warehouses(Id),
    BinCode nvarchar(50) NOT NULL,
    FullPath nvarchar(500) NOT NULL,
    IsActive bit NOT NULL DEFAULT 1,
    CreatedAt datetime2 NOT NULL DEFAULT sysutcdatetime(),
    CreatedBy nvarchar(100) NOT NULL,
    UpdatedAt datetime2 NULL,
    UpdatedBy nvarchar(100) NULL,
    CONSTRAINT UQ_BinLocations_Warehouse_Code UNIQUE (WarehouseId, BinCode)
);

CREATE TABLE ItemCategories (
    Id int IDENTITY(1,1) PRIMARY KEY,
    CategoryCode nvarchar(50) NOT NULL UNIQUE,
    Name nvarchar(255) NOT NULL,
    IsActive bit NOT NULL DEFAULT 1,
    CreatedAt datetime2 NOT NULL DEFAULT sysutcdatetime(),
    CreatedBy nvarchar(100) NOT NULL,
    UpdatedAt datetime2 NULL,
    UpdatedBy nvarchar(100) NULL
);

CREATE TABLE ItemUnits (
    Id int IDENTITY(1,1) PRIMARY KEY,
    UnitCode nvarchar(50) NOT NULL UNIQUE,
    Name nvarchar(255) NOT NULL,
    IsActive bit NOT NULL DEFAULT 1,
    CreatedAt datetime2 NOT NULL DEFAULT sysutcdatetime(),
    CreatedBy nvarchar(100) NOT NULL,
    UpdatedAt datetime2 NULL,
    UpdatedBy nvarchar(100) NULL
);

CREATE TABLE Items (
    Id int IDENTITY(1,1) PRIMARY KEY,
    ItemCode nvarchar(100) NOT NULL UNIQUE,
    DefaultName nvarchar(255) NOT NULL,
    CategoryId int NOT NULL REFERENCES ItemCategories(Id),
    UnitId int NOT NULL REFERENCES ItemUnits(Id),
    IsSerialManaged bit NOT NULL,
    IsActive bit NOT NULL DEFAULT 1,
    CreatedAt datetime2 NOT NULL DEFAULT sysutcdatetime(),
    CreatedBy nvarchar(100) NOT NULL,
    UpdatedAt datetime2 NULL,
    UpdatedBy nvarchar(100) NULL
);

CREATE TABLE ItemInstances (
    Id int IDENTITY(1,1) PRIMARY KEY,
    ItemId int NOT NULL REFERENCES Items(Id),
    SerialNumber nvarchar(150) NULL,
    Barcode nvarchar(150) NULL,
    Status int NOT NULL,
    IsActive bit NOT NULL DEFAULT 1,
    CreatedAt datetime2 NOT NULL DEFAULT sysutcdatetime(),
    CreatedBy nvarchar(100) NOT NULL,
    UpdatedAt datetime2 NULL,
    UpdatedBy nvarchar(100) NULL
);

CREATE UNIQUE INDEX IX_ItemInstances_SerialNumber ON ItemInstances(SerialNumber) WHERE SerialNumber IS NOT NULL;
CREATE UNIQUE INDEX IX_ItemInstances_Barcode ON ItemInstances(Barcode) WHERE Barcode IS NOT NULL;

CREATE TABLE ExternalParties (
    Id int IDENTITY(1,1) PRIMARY KEY,
    PartyCode nvarchar(100) NOT NULL,
    Name nvarchar(255) NOT NULL,
    PartyType int NOT NULL,
    ContactName nvarchar(255) NULL,
    Phone nvarchar(50) NULL,
    Email nvarchar(255) NULL,
    IsActive bit NOT NULL DEFAULT 1,
    CreatedAt datetime2 NOT NULL DEFAULT sysutcdatetime(),
    CreatedBy nvarchar(100) NOT NULL,
    UpdatedAt datetime2 NULL,
    UpdatedBy nvarchar(100) NULL,
    CONSTRAINT UQ_ExternalParties_Type_Code UNIQUE (PartyType, PartyCode)
);

CREATE TABLE CurrentItemLocations (
    Id int IDENTITY(1,1) PRIMARY KEY,
    ItemInstanceId int NOT NULL UNIQUE REFERENCES ItemInstances(Id),
    LocationType int NOT NULL,
    WarehouseId int NULL REFERENCES Warehouses(Id),
    BinLocationId int NULL REFERENCES BinLocations(Id),
    ExternalPartyId int NULL REFERENCES ExternalParties(Id),
    ReferenceDocumentType nvarchar(100) NULL,
    ReferenceDocumentId int NULL,
    ReferenceDocumentNo nvarchar(100) NULL,
    UpdatedLocationAt datetime2 NOT NULL,
    UpdatedLocationBy nvarchar(100) NOT NULL,
    CreatedAt datetime2 NOT NULL DEFAULT sysutcdatetime(),
    CreatedBy nvarchar(100) NOT NULL,
    UpdatedAt datetime2 NULL,
    UpdatedBy nvarchar(100) NULL
);

CREATE TABLE ItemMovementHistories (
    Id bigint IDENTITY(1,1) PRIMARY KEY,
    ItemInstanceId int NOT NULL REFERENCES ItemInstances(Id),
    ActionType int NOT NULL,
    FromLocationType int NULL,
    FromLocationId int NULL,
    FromLocationDisplay nvarchar(500) NULL,
    ToLocationType int NULL,
    ToLocationId int NULL,
    ToLocationDisplay nvarchar(500) NULL,
    OldStatus int NOT NULL,
    NewStatus int NOT NULL,
    DocumentType nvarchar(100) NOT NULL,
    DocumentId int NOT NULL,
    DocumentNo nvarchar(100) NOT NULL,
    Note nvarchar(1000) NULL,
    PerformedAt datetime2 NOT NULL,
    PerformedBy nvarchar(100) NOT NULL
);

CREATE TABLE StockBalances (
    Id int IDENTITY(1,1) PRIMARY KEY,
    WarehouseId int NOT NULL REFERENCES Warehouses(Id),
    BinLocationId int NULL REFERENCES BinLocations(Id),
    ItemId int NOT NULL REFERENCES Items(Id),
    Status int NOT NULL,
    Quantity decimal(18,4) NOT NULL,
    CreatedAt datetime2 NOT NULL DEFAULT sysutcdatetime(),
    CreatedBy nvarchar(100) NOT NULL,
    UpdatedAt datetime2 NULL,
    UpdatedBy nvarchar(100) NULL
);

CREATE TABLE InventoryTransactions (
    Id bigint IDENTITY(1,1) PRIMARY KEY,
    TransactionType int NOT NULL,
    ItemId int NOT NULL REFERENCES Items(Id),
    ItemInstanceId int NULL REFERENCES ItemInstances(Id),
    WarehouseId int NULL REFERENCES Warehouses(Id),
    BinLocationId int NULL REFERENCES BinLocations(Id),
    QuantityDelta decimal(18,4) NOT NULL,
    StatusAfter int NOT NULL,
    DocumentType nvarchar(100) NOT NULL,
    DocumentId int NOT NULL,
    DocumentNo nvarchar(100) NOT NULL,
    PostedAt datetime2 NOT NULL,
    PostedBy nvarchar(100) NOT NULL
);

CREATE INDEX IX_Items_ItemCode ON Items(ItemCode);
CREATE INDEX IX_CurrentItemLocations_ItemInstanceId ON CurrentItemLocations(ItemInstanceId);
CREATE INDEX IX_ItemMovementHistories_ItemInstanceId ON ItemMovementHistories(ItemInstanceId);
CREATE INDEX IX_StockBalances_Warehouse_Item ON StockBalances(WarehouseId, ItemId);
CREATE INDEX IX_InventoryTransactions_Document ON InventoryTransactions(DocumentType, DocumentId);

-- Document tables are generated by EF Code First from:
-- InboundDocuments/Lines, MoveDocuments/Lines, RepairDocuments/Lines,
-- BorrowDocuments/Lines, AdjustmentDocuments/Lines, InventoryCheckDocuments/Lines,
-- ImportBatches/Rows, Attachments, AuditLogs, Translations, Notifications,
-- UserWarehousePermissions.
