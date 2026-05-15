using ERP.Inventory.Application.Common;
using ERP.Inventory.Application.Interfaces;
using ERP.Inventory.Domain.Entities;
using ERP.Inventory.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ERP.Inventory.Web.Controllers;

/// <summary>User, Role, and warehouse permission management</summary>
[Route("[controller]")]
[Authorize(Roles = "Admin")]
public sealed class UserManagementController : ManagementBaseController
{
    public UserManagementController(InventoryDbContext db, ICurrentUserService currentUserService)
        : base(db, currentUserService) { }

    [HttpGet("Roles")]
    public async Task<IActionResult> Roles(CancellationToken cancellationToken)
    {
        var rows = await Db.SystemRoles.AsNoTracking().OrderBy(x => x.Id).Select(x => new { x.Id, x.Name, x.Description }).ToArrayAsync(cancellationToken);
        return Json(rows);
    }

    [HttpGet("Users")]
    public async Task<IActionResult> Users([FromQuery] string? keyword = null, [FromQuery] bool? isActive = null, CancellationToken cancellationToken = default)
    {
        var key = keyword?.Trim();
        var query = Db.SystemUsers.AsNoTracking().Include(x => x.UserRoles).ThenInclude(x => x.Role).AsQueryable();
        if (isActive.HasValue) query = query.Where(x => x.IsActive == isActive.Value);
        if (!string.IsNullOrWhiteSpace(key)) query = query.Where(x => x.UserName.Contains(key) || x.DisplayName.Contains(key) || x.Email.Contains(key));

        var users = await query.OrderBy(x => x.UserName).Take(100).ToArrayAsync(cancellationToken);
        var userIds = users.Select(x => x.Id).ToArray();
        var permissions = await Db.UserWarehousePermissions.AsNoTracking().Include(x => x.Warehouse).Where(x => userIds.Contains(x.UserId)).ToArrayAsync(cancellationToken);

        var rows = users.Select(x => new
        {
            x.Id, x.UserName, x.DisplayName, x.Email, x.PreferredLanguage, x.IsActive,
            Roles = x.UserRoles.Select(r => r.Role != null ? r.Role.Name : string.Empty).Where(r => r.Length > 0).OrderBy(r => r).ToArray(),
            Warehouses = permissions.Where(p => p.UserId == x.Id).Select(p => p.Warehouse != null ? p.Warehouse.WarehouseCode : p.WarehouseId.ToString()).OrderBy(p => p).ToArray()
        });
        return Json(rows);
    }

    [HttpGet("User/{id}")]
    public async Task<IActionResult> UserDetail(string id, CancellationToken cancellationToken)
    {
        var user = await Db.SystemUsers.AsNoTracking().Include(x => x.UserRoles).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (user == null) return NotFound();
        var warehouseIds = await Db.UserWarehousePermissions.AsNoTracking().Where(x => x.UserId == id && x.CanView).Select(x => x.WarehouseId).ToArrayAsync(cancellationToken);
        return Json(new { user.Id, user.UserName, user.DisplayName, user.Email, user.PreferredLanguage, user.IsActive, RoleIds = user.UserRoles.Select(x => x.RoleId).ToArray(), WarehouseIds = warehouseIds });
    }

    [HttpPost("User")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser([FromBody] UserRequest request, CancellationToken cancellationToken)
    {
        var validation = ValidateRequired(("UserName", request.UserName), ("DisplayName", request.DisplayName), ("Password", request.Password));
        if (validation != null) return validation;

        var normalized = request.UserName.Trim().ToUpperInvariant();
        if (await Db.SystemUsers.AnyAsync(x => x.NormalizedUserName == normalized, cancellationToken))
            return Json(new { success = false, message = "User name already exists." });

        var user = new SystemUser
        {
            UserName = request.UserName.Trim(), NormalizedUserName = normalized,
            DisplayName = request.DisplayName.Trim(), Email = request.Email?.Trim() ?? string.Empty,
            PreferredLanguage = NormalizeLanguage(request.PreferredLanguage),
            PasswordHash = PasswordHashService.Hash(request.Password ?? string.Empty),
            IsActive = request.IsActive, CreatedAt = DateTime.UtcNow
        };
        Db.SystemUsers.Add(user);
        await Db.SaveChangesAsync(cancellationToken);

        var accessError = await ReplaceUserAccessAsync(user.Id, request.RoleIds, request.WarehouseIds, cancellationToken);
        if (accessError != null) { Db.SystemUsers.Remove(user); await Db.SaveChangesAsync(cancellationToken); return Json(new { success = false, message = accessError }); }

        AddAudit("Create", nameof(SystemUser), null, user.UserName);
        await Db.SaveChangesAsync(cancellationToken);
        return Json(new { success = true, id = user.Id });
    }

    [HttpPut("User/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateUser(string id, [FromBody] UserRequest request, CancellationToken cancellationToken)
    {
        var validation = ValidateRequired(("UserName", request.UserName), ("DisplayName", request.DisplayName));
        if (validation != null) return validation;

        var user = await Db.SystemUsers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (user == null) return NotFound();

        var normalized = request.UserName.Trim().ToUpperInvariant();
        if (await Db.SystemUsers.AnyAsync(x => x.Id != id && x.NormalizedUserName == normalized, cancellationToken))
            return Json(new { success = false, message = "User name already exists." });

        if (id == CurrentUserService.GetCurrentUser().UserId)
        {
            var selectedRoleNames = await Db.SystemRoles.AsNoTracking().Where(x => request.RoleIds.Contains(x.Id)).Select(x => x.Name).ToArrayAsync(cancellationToken);
            if (!selectedRoleNames.Contains("Admin", StringComparer.OrdinalIgnoreCase) || !request.IsActive)
                return Json(new { success = false, message = "You cannot remove your own admin access." });
        }

        user.UserName = request.UserName.Trim(); user.NormalizedUserName = normalized;
        user.DisplayName = request.DisplayName.Trim(); user.Email = request.Email?.Trim() ?? string.Empty;
        user.PreferredLanguage = NormalizeLanguage(request.PreferredLanguage); user.IsActive = request.IsActive;
        if (!string.IsNullOrWhiteSpace(request.Password)) user.PasswordHash = PasswordHashService.Hash(request.Password);

        var accessError = await ReplaceUserAccessAsync(user.Id, request.RoleIds, request.WarehouseIds, cancellationToken);
        if (accessError != null) return Json(new { success = false, message = accessError });

        AddAudit("Update", nameof(SystemUser), null, user.UserName);
        await Db.SaveChangesAsync(cancellationToken);
        return Json(new { success = true });
    }

    [HttpPost("User/{id}/Deactivate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeactivateUser(string id, CancellationToken cancellationToken)
    {
        if (id == CurrentUserService.GetCurrentUser().UserId) return Json(new { success = false, message = "You cannot deactivate your own account." });
        var user = await Db.SystemUsers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (user == null) return NotFound();
        user.IsActive = false; AddAudit("SoftDelete", nameof(SystemUser), null, user.UserName);
        await Db.SaveChangesAsync(cancellationToken);
        return Json(new { success = true });
    }

    [HttpPost("User/{id}/Restore")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestoreUser(string id, CancellationToken cancellationToken)
    {
        var user = await Db.SystemUsers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (user == null) return NotFound();
        user.IsActive = true; AddAudit("Restore", nameof(SystemUser), null, user.UserName);
        await Db.SaveChangesAsync(cancellationToken);
        return Json(new { success = true });
    }

    [HttpDelete("User/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(string id, CancellationToken cancellationToken)
    {
        if (id == CurrentUserService.GetCurrentUser().UserId) return Json(new { success = false, message = "You cannot hard delete your own account." });
        var user = await Db.SystemUsers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (user == null) return NotFound();
        Db.SystemUserRoles.RemoveRange(Db.SystemUserRoles.Where(x => x.UserId == id));
        Db.UserWarehousePermissions.RemoveRange(Db.UserWarehousePermissions.Where(x => x.UserId == id));
        Db.Notifications.RemoveRange(Db.Notifications.Where(x => x.UserId == id));
        Db.SystemUsers.Remove(user); AddAudit("HardDelete", nameof(SystemUser), null, user.UserName);
        return await TrySaveDeleteAsync(cancellationToken);
    }

    private async Task<string?> ReplaceUserAccessAsync(string userId, IReadOnlyCollection<int> roleIds, IReadOnlyCollection<int> warehouseIds, CancellationToken cancellationToken)
    {
        var distinctRoleIds = roleIds.Distinct().ToArray();
        if (distinctRoleIds.Length == 0) return "At least one role is required.";
        var roles = await Db.SystemRoles.Where(x => distinctRoleIds.Contains(x.Id)).ToArrayAsync(cancellationToken);
        if (roles.Length != distinctRoleIds.Length) return "One or more roles are invalid.";
        var isAdminRole = roles.Any(x => x.Name.Equals("Admin", StringComparison.OrdinalIgnoreCase));
        var canManage = isAdminRole || roles.Any(x => x.Name.Equals("Warehouse Manager", StringComparison.OrdinalIgnoreCase));
        var canOperate = canManage || roles.Any(x => x.Name.Equals("Warehouse Staff", StringComparison.OrdinalIgnoreCase));
        var distinctWarehouseIds = warehouseIds.Distinct().Where(x => x > 0).ToArray();
        if (!isAdminRole && distinctWarehouseIds.Length == 0) return "Non-admin users must be assigned to at least one warehouse.";
        var validWarehouseIds = await Db.Warehouses.Where(x => distinctWarehouseIds.Contains(x.Id) && x.IsActive).Select(x => x.Id).ToArrayAsync(cancellationToken);
        if (validWarehouseIds.Length != distinctWarehouseIds.Length) return "One or more warehouses are invalid.";

        Db.SystemUserRoles.RemoveRange(Db.SystemUserRoles.Where(x => x.UserId == userId));
        Db.UserWarehousePermissions.RemoveRange(Db.UserWarehousePermissions.Where(x => x.UserId == userId));
        await Db.SaveChangesAsync(cancellationToken);

        foreach (var role in roles) Db.SystemUserRoles.Add(new SystemUserRole { UserId = userId, RoleId = role.Id });
        foreach (var warehouseId in validWarehouseIds)
            Db.UserWarehousePermissions.Add(new UserWarehousePermission { UserId = userId, WarehouseId = warehouseId, CanView = true, CanOperate = canOperate, CanManage = canManage, CreatedBy = CurrentUserService.GetCurrentUser().UserName });
        return null;
    }

    public sealed class UserRequest
    {
        public string UserName { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string? Email { get; init; }
        public string? Password { get; init; }
        public string? PreferredLanguage { get; init; } = "vi";
        public bool IsActive { get; init; } = true;
        public IReadOnlyCollection<int> RoleIds { get; init; } = Array.Empty<int>();
        public IReadOnlyCollection<int> WarehouseIds { get; init; } = Array.Empty<int>();
    }
}
