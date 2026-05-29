using ERP.Inventory.Infrastructure.Data;
using ERP.Inventory.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ERP.Inventory.Web.Controllers;

//[Authorize(Policy = "SuperOnly")]
[Route("[controller]")]
public sealed class SystemErrorsController : Controller
{
    private readonly InventoryDbContext _db;

    public SystemErrorsController(InventoryDbContext db)
    {
        _db = db;
    }

    [HttpGet("Status")]
    public IActionResult Status()
        => Json(new { passwordConfigured = !string.IsNullOrWhiteSpace(SuperAdminSecurity.SuperAdminPassword) });

    [HttpPost("Search")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Search([FromBody] ErrorSearchRequest request, CancellationToken cancellationToken)
    {

        var page = Math.Max(1, request.Page);
        var pageSize = request.PageSize == 0 ? 0 : Math.Clamp(request.PageSize, 1, 200);
        var query = _db.LogErrorSystems.AsNoTracking().AsQueryable();

        if (request.IsResolved.HasValue) query = query.Where(x => x.IsResolved == request.IsResolved.Value);
        if (request.FromDate.HasValue) query = query.Where(x => x.CreatedAt >= request.FromDate.Value);
        if (request.ToDate.HasValue)
        {
            var to = request.ToDate.Value.Date.AddDays(1);
            query = query.Where(x => x.CreatedAt < to);
        }
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(x =>
                x.ErrorCode.Contains(keyword) ||
                (x.UserName != null && x.UserName.Contains(keyword)) ||
                (x.RequestPath != null && x.RequestPath.Contains(keyword)) ||
                (x.Module != null && x.Module.Contains(keyword)) ||
                (x.Action != null && x.Action.Contains(keyword)) ||
                x.ErrorMessage.Contains(keyword));
        }

        var total = await query.CountAsync(cancellationToken);
        if (pageSize == 0) pageSize = total == 0 ? 1 : total;
        var rows = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Id,
                x.ErrorCode,
                x.CreatedAt,
                x.UserName,
                x.RequestPath,
                x.HttpMethod,
                x.Module,
                x.Action,
                x.ErrorMessage,
                x.IsResolved,
                x.ResolvedAt,
                x.ResolvedBy
            })
            .ToArrayAsync(cancellationToken);

        return Json(new { success = true, items = rows, page, pageSize, totalCount = total });
    }

    [HttpPost("{id:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Detail(long id, [FromBody] SuperAdminRequest request, CancellationToken cancellationToken)
    {

        var row = await _db.LogErrorSystems.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (row == null) return NotFound(new { success = false, message = "Error log not found." });

        return Json(new
        {
            success = true,
            data = new
            {
                row.Id,
                row.ErrorCode,
                row.CreatedAt,
                row.UserId,
                row.UserName,
                row.RequestPath,
                row.HttpMethod,
                row.Module,
                row.Action,
                row.ErrorMessage,
                row.InnerException,
                row.StackTrace,
                row.PayloadJson,
                row.ClientIp,
                row.Browser,
                row.IsResolved,
                row.ResolvedAt,
                row.ResolvedBy,
                row.Notes
            }
        });
    }

    [HttpPost("{id:long}/Resolve")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Resolve(long id, [FromBody] ResolveErrorRequest request, CancellationToken cancellationToken)
    {

        var row = await _db.LogErrorSystems.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (row == null) return NotFound(new { success = false, message = "Error log not found." });

        row.IsResolved = true;
        row.ResolvedAt = DateTime.UtcNow;
        row.ResolvedBy = User.Identity?.Name ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        row.Notes = request.Notes;
        await _db.SaveChangesAsync(cancellationToken);

        return Json(new { success = true });
    }

    public class SuperAdminRequest
    {
        public string? SuperAdminPassword { get; init; }
    }

    public sealed class ErrorSearchRequest : SuperAdminRequest
    {
        public int Page { get; init; } = 1;
        public int PageSize { get; init; } = 25;
        public bool? IsResolved { get; init; }
        public DateTime? FromDate { get; init; }
        public DateTime? ToDate { get; init; }
        public string? Keyword { get; init; }
    }

    public sealed class ResolveErrorRequest : SuperAdminRequest
    {
        public string? Notes { get; init; }
    }
}
