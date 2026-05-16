using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ERP.Inventory.Infrastructure.Data;

public sealed class InventoryDbContextFactory : IDesignTimeDbContextFactory<InventoryDbContext>
{
    public InventoryDbContext CreateDbContext(string[] args)
    {
        const string connectionString = "Server=DESKTOP-8895O66;Database=WarehouseManager;User ID=sa;Password=foxconn168;TrustServerCertificate=True;MultipleActiveResultSets=true";
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new InventoryDbContext(options);
    }
}
