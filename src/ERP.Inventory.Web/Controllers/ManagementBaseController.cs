using ERP.Inventory.Application.Interfaces;
using ERP.Inventory.Domain.Common;
using ERP.Inventory.Domain.Entities;
using ERP.Inventory.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ERP.Inventory.Web.Controllers;

/// <summary>
/// Shared helpers for management controllers: Touch, AddAudit, ValidateRequired, etc.
/// </summary>
[Authorize]
public abstract class ManagementBaseController : Controller
{
    protected readonly InventoryDbContext Db;
    protected readonly ICurrentUserService CurrentUserService;

    protected ManagementBaseController(InventoryDbContext db, ICurrentUserService currentUserService)
    {
        Db = db;
        CurrentUserService = currentUserService;
    }

    protected IActionResult? ValidateRequired(params (string Field, string? Value)[] fields)
    {
        var missing = fields.Where(x => string.IsNullOrWhiteSpace(x.Value)).Select(x => $"{x.Field} is required.").ToArray();
        return missing.Length == 0 ? null : Json(new { success = false, message = string.Join(" ", missing), errors = missing });
    }

    protected void Touch(AuditableEntity entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = CurrentUserService.GetCurrentUser().UserName;
    }

    protected void AddAudit(string action, string entityName, int? entityId, string? referenceNo)
    {
        var user = CurrentUserService.GetCurrentUser();
        Db.AuditLogs.Add(new AuditLog
        {
            UserId = user.UserId,
            UserName = user.UserName,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            ReferenceNo = referenceNo,
            Result = "Success",
            CreatedAt = DateTime.UtcNow
        });
    }

    protected async Task<IActionResult> TrySaveDeleteAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Db.SaveChangesAsync(cancellationToken);
            return Json(new { success = true });
        }
        catch (DbUpdateException)
        {
            return Json(new { success = false, message = "Cannot hard delete this record because it is referenced by operational data. Use soft delete instead." });
        }
    }

    protected static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    protected static string NormalizeLanguage(string? language)
    {
        return language?.ToLowerInvariant() switch
        {
            "en" => "en",
            "zh" => "zh",
            _ => "vi"
        };
    }
}
