using ERP.Inventory.Application.Interfaces;
using ERP.Inventory.Infrastructure;
using ERP.Inventory.Infrastructure.Data;
using ERP.Inventory.Infrastructure.Seed;
using ERP.Inventory.Infrastructure.Services;
using ERP.Inventory.Web.Middleware;
using ERP.Inventory.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
SuperAdminSecurity.SuperAdminPassword = builder.Configuration["SuperAdminPassword"] ?? SuperAdminSecurity.SuperAdminPassword;

// Add services to the container.
var dataProtectionPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "DataProtectionKeys");
Directory.CreateDirectory(dataProtectionPath);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath))
    .SetApplicationName("ERP.Inventory");

builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddInventoryInfrastructure(builder.Configuration);
builder.Services.AddSingleton<IAuthorizationHandler, SuperAdminAuthorizationHandler>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "B34G_Warehouse_Auth";
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SuperOnly", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("AuthMode", "Super");
        policy.RequireRole("SystemSuperAdmin");
        policy.RequireClaim(System.Security.Claims.ClaimTypes.Name, "SuperAdmin");
    });
});

builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "B34G_Warehouse_Antiforgery";
    options.HeaderName = "RequestVerificationToken";
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<LogErrorSystemMiddleware>();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Erp}/{action=Index}/{id?}");

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();

    try
    {
        // Auto migrate database
        await db.Database.MigrateAsync();

        // Seed security data
        if (builder.Configuration.GetValue<bool>("SeedSecurityData", true))
        {
            await SecuritySeedData.SeedAsync(db);
        }

        // Seed sample inventory data ONLY in Development
        if (app.Environment.IsDevelopment() &&
            builder.Configuration.GetValue<bool>("SeedDatabase"))
        {
            await InventorySeedData.SeedAsync(db);
        }
    }
    catch (Exception ex)
    {
        try
        {
            var errorLog = scope.ServiceProvider.GetRequiredService<ILogErrorSystemService>();
            await errorLog.LogAsync(ex, new LogErrorContext(Module: "Startup", Action: "MigrateOrSeed"));
        }
        catch
        {
            // Database may be unavailable or the error table may not exist yet.
            app.Logger.LogError(ex, "Startup migration/seed failed before LogErrorSystem could persist the exception.");
        }

        throw;
    }
}

app.Run();
