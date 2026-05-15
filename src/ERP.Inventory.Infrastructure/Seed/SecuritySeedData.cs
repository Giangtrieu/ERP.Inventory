using ERP.Inventory.Application.Common;
using ERP.Inventory.Domain.Entities;
using ERP.Inventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ERP.Inventory.Infrastructure.Seed;

public static class SecuritySeedData
{
    public static async Task SeedAsync(InventoryDbContext db, CancellationToken cancellationToken = default)
    {
        if (!await db.SystemRoles.AnyAsync(cancellationToken))
        {
            db.SystemRoles.AddRange(
                new SystemRole { Name = "Admin", NormalizedName = "ADMIN", Description = "Toan quyen he thong" },
                new SystemRole { Name = "Warehouse Manager", NormalizedName = "WAREHOUSE MANAGER", Description = "Quan ly kho theo pham vi duoc gan" },
                new SystemRole { Name = "Warehouse Staff", NormalizedName = "WAREHOUSE STAFF", Description = "Thao tac nghiep vu kho" },
                new SystemRole { Name = "Viewer", NormalizedName = "VIEWER", Description = "Chi xem du lieu va bao cao" });
            await db.SaveChangesAsync(cancellationToken);
        }

        await EnsureUserAsync(db, "admin", "admin@erp.local", "System Admin", "Admin", cancellationToken);
        await EnsureUserAsync(db, "manager", "manager@erp.local", "Warehouse Manager", "Warehouse Manager", cancellationToken);
        await EnsureUserAsync(db, "staff", "staff@erp.local", "Warehouse Staff", "Warehouse Staff", cancellationToken);
        await EnsureUserAsync(db, "viewer", "viewer@erp.local", "Viewer", "Viewer", cancellationToken);
    }

    private static async Task EnsureUserAsync(InventoryDbContext db, string userName, string email, string displayName, string roleName, CancellationToken cancellationToken)
    {
        var normalized = userName.ToUpperInvariant();
        var user = await db.SystemUsers.FirstOrDefaultAsync(x => x.NormalizedUserName == normalized, cancellationToken);
        if (user == null)
        {
            user = new SystemUser
            {
                UserName = userName,
                NormalizedUserName = normalized,
                Email = email,
                DisplayName = displayName,
                PasswordHash = PasswordHashService.Hash("123456"),
                PreferredLanguage = "vi",
                CreatedAt = DateTime.UtcNow
            };
            db.SystemUsers.Add(user);
            await db.SaveChangesAsync(cancellationToken);
        }

        var role = await db.SystemRoles.FirstAsync(x => x.Name == roleName, cancellationToken);
        var hasRole = await db.SystemUserRoles.AnyAsync(x => x.UserId == user.Id && x.RoleId == role.Id, cancellationToken);
        if (!hasRole)
        {
            db.SystemUserRoles.Add(new SystemUserRole { UserId = user.Id, RoleId = role.Id });
        }

        var firstWarehouse = await db.Warehouses.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(cancellationToken);
        if (firstWarehouse != null)
        {
            var hasWarehouse = await db.UserWarehousePermissions.AnyAsync(x => x.UserId == user.Id && x.WarehouseId == firstWarehouse.Id, cancellationToken);
            if (!hasWarehouse)
            {
                db.UserWarehousePermissions.Add(new UserWarehousePermission
                {
                    UserId = user.Id,
                    WarehouseId = firstWarehouse.Id,
                    CanView = true,
                    CanOperate = roleName is "Admin" or "Warehouse Manager" or "Warehouse Staff",
                    CanManage = roleName is "Admin" or "Warehouse Manager",
                    CreatedBy = "seed"
                });
            }
        }

        var hasNotification = await db.Notifications.AnyAsync(x => x.UserId == user.Id, cancellationToken);
        if (!hasNotification)
        {
            db.Notifications.Add(new Notification
            {
                UserId = user.Id,
                Title = "Welcome to ERP WMS",
                Message_Vi = $"Tài khoản {userName} đã sẵn sàng với vai trò {roleName}.",
                Message_En = $"Account {userName} is ready with role {roleName}.",
                Message_Zh = $"账号 {userName} 已准备好担任该角色 {roleName}.",
                LinkUrl = "/",
                CreatedBy = "seed"
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
