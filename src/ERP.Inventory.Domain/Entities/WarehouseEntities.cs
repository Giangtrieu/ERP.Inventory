using ERP.Inventory.Domain.Common;

namespace ERP.Inventory.Domain.Entities;

public class Company : AuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public ICollection<Branch> Branches { get; set; } = new List<Branch>();
}

public class Branch : AuditableEntity
{
    public int CompanyId { get; set; }
    public Company? Company { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public ICollection<Warehouse> Warehouses { get; set; } = new List<Warehouse>();
}

public class Warehouse : AuditableEntity
{
    public int BranchId { get; set; }
    public Branch? Branch { get; set; }
    public string WarehouseCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public ICollection<WarehouseZone> Zones { get; set; } = new List<WarehouseZone>();
}

public class WarehouseZone : AuditableEntity
{
    public int WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public string ZoneCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public ICollection<Rack> Racks { get; set; } = new List<Rack>();
}

public class Rack : AuditableEntity
{
    public int WarehouseZoneId { get; set; }
    public WarehouseZone? WarehouseZone { get; set; }
    public string RackCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public ICollection<Shelf> Shelves { get; set; } = new List<Shelf>();
}

public class Shelf : AuditableEntity
{
    public int RackId { get; set; }
    public Rack? Rack { get; set; }
    public string ShelfCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public ICollection<BinLocation> BinLocations { get; set; } = new List<BinLocation>();
}

public class BinLocation : AuditableEntity
{
    public int ShelfId { get; set; }
    public Shelf? Shelf { get; set; }
    public int WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public string BinCode { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

