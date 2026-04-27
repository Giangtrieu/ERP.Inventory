using ERP.Inventory.Application.Interfaces;
using ERP.Inventory.Application.Services;
using ERP.Inventory.Infrastructure.Data;
using ERP.Inventory.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Inventory.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInventoryInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("InventoryDb")
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=ERPInventoryDb;Trusted_Connection=True;MultipleActiveResultSets=true";

        services.AddDbContext<InventoryDbContext>(options => options.UseSqlServer(connectionString));

        services.AddScoped<IInventoryStatePolicy, InventoryStatePolicy>();
        services.AddScoped<IDocumentNumberService, DocumentNumberService>();
        services.AddScoped<ITrackingService, TrackingService>();
        services.AddScoped<IInboundService, InventoryOperationService>();
        services.AddScoped<IInventoryOperationService, InventoryOperationService>();
        services.AddScoped<IRepairService, InventoryOperationService>();
        services.AddScoped<IBorrowService, InventoryOperationService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IImportService, ImportExportService>();
        services.AddScoped<IExportService, ImportExportService>();

        return services;
    }
}
