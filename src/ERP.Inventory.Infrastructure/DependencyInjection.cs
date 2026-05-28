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

        // Core abstractions
        services.AddSingleton<IDateTimeProvider, UtcDateTimeProvider>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();

        // Business policies
        services.AddScoped<IInventoryStatePolicy, InventoryStatePolicy>();
        services.AddScoped<IDocumentNumberService, DocumentNumberService>();

        // Tracking & Dashboard
        services.AddScoped<ITrackingService, TrackingService>();
        services.AddScoped<IDashboardService, DashboardService>();

        // Inventory operations — each service is now separate
        services.AddScoped<IInboundService, InboundService>();
        services.AddScoped<IInventoryOperationService, MoveLocationService>();
        services.AddScoped<IRepairService, RepairServiceImpl>();
        services.AddScoped<IBorrowService, BorrowServiceImpl>();
        services.AddScoped<IQuantityInventoryService, QuantityInventoryService>();
        services.AddScoped<IDocumentRollbackService, DocumentRollbackService>();
        services.AddScoped<IDocumentLifecycleService, DocumentLifecycleService>();
        services.AddScoped<InventoryCheckService>();
        services.AddScoped<AdjustmentService>();
        services.AddScoped<ReconciliationService>();

        // Import / Export
        services.AddScoped<IImportService, ImportExportService>();
        services.AddScoped<IExportService, ImportExportService>();

        return services;
    }
}
