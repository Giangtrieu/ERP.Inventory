using ERP.Inventory.Application.Interfaces;
using ERP.Inventory.Domain.Entities;
using ERP.Inventory.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ERP.Inventory.Web.Controllers;

[Authorize]
[Route("[controller]")]
public sealed class AttachmentsController : Controller
{
    private const long MaxFileSize = 10 * 1024 * 1024;
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".jpg", ".jpeg", ".png", ".xlsx", ".xls", ".docx"
    };

    private static readonly HashSet<string> AllowedEntityNames = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(InboundDocument),
        nameof(MoveDocument),
        nameof(RepairDocument),
        nameof(BorrowDocument),
        nameof(AdjustmentDocument),
        nameof(InventoryCheckDocument)
    };

    private readonly InventoryDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly IWebHostEnvironment _environment;

    public AttachmentsController(InventoryDbContext db, ICurrentUserService currentUserService, IWebHostEnvironment environment)
    {
        _db = db;
        _currentUserService = currentUserService;
        _environment = environment;
    }

    [HttpGet("List")]
    public async Task<IActionResult> List([FromQuery] string entityName, [FromQuery] int entityId, CancellationToken cancellationToken)
    {
        if (!IsValidEntity(entityName, entityId))
        {
            return Json(Array.Empty<object>());
        }

        var rows = await _db.Attachments.AsNoTracking()
            .Where(x => x.EntityName == entityName && x.EntityId == entityId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new { x.Id, x.FileName, x.ContentType, x.FileSize, x.CreatedAt, x.CreatedBy })
            .ToArrayAsync(cancellationToken);

        return Json(rows);
    }

    [HttpPost("Upload")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload([FromForm] string entityName, [FromForm] int entityId, [FromForm] IFormFile? file, CancellationToken cancellationToken)
    {
        if (!IsValidEntity(entityName, entityId) || !await EntityExistsAsync(entityName, entityId, cancellationToken))
        {
            return Json(new { success = false, message = "Attachment entity is invalid." });
        }

        if (file == null || file.Length <= 0)
        {
            return Json(new { success = false, message = "File is empty." });
        }

        if (file.Length > MaxFileSize)
        {
            return Json(new { success = false, message = "File is too large." });
        }

        var originalFileName = Path.GetFileName(file.FileName);
        var extension = Path.GetExtension(originalFileName);
        if (!AllowedExtensions.Contains(extension))
        {
            return Json(new { success = false, message = "File type is not allowed." });
        }

        var safeFileName = string.Concat(originalFileName.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
        var entityFolder = Path.Combine(_environment.ContentRootPath, "App_Data", "attachments", entityName, entityId.ToString());
        Directory.CreateDirectory(entityFolder);

        var storedFileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(entityFolder, storedFileName);
        await using (var stream = System.IO.File.Create(fullPath))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        var user = _currentUserService.GetCurrentUser();
        var relativePath = Path.Combine("App_Data", "attachments", entityName, entityId.ToString(), storedFileName);
        _db.Attachments.Add(new Attachment
        {
            EntityName = entityName,
            EntityId = entityId,
            FileName = safeFileName,
            FilePath = relativePath,
            ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
            FileSize = file.Length,
            CreatedBy = user.UserName
        });

        await _db.SaveChangesAsync(cancellationToken);
        return Json(new { success = true, message = "Attachment uploaded." });
    }

    [HttpGet("Download/{id:int}")]
    public async Task<IActionResult> Download(int id, CancellationToken cancellationToken)
    {
        var attachment = await _db.Attachments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (attachment == null)
        {
            return NotFound();
        }

        var fullPath = Path.Combine(_environment.ContentRootPath, attachment.FilePath);
        if (!System.IO.File.Exists(fullPath))
        {
            return NotFound();
        }

        return PhysicalFile(fullPath, attachment.ContentType, attachment.FileName);
    }

    private static bool IsValidEntity(string? entityName, int entityId)
    {
        return entityId > 0 && !string.IsNullOrWhiteSpace(entityName) && AllowedEntityNames.Contains(entityName);
    }

    private Task<bool> EntityExistsAsync(string entityName, int entityId, CancellationToken cancellationToken)
    {
        return entityName switch
        {
            nameof(InboundDocument) => _db.InboundDocuments.AnyAsync(x => x.Id == entityId, cancellationToken),
            nameof(MoveDocument) => _db.MoveDocuments.AnyAsync(x => x.Id == entityId, cancellationToken),
            nameof(RepairDocument) => _db.RepairDocuments.AnyAsync(x => x.Id == entityId, cancellationToken),
            nameof(BorrowDocument) => _db.BorrowDocuments.AnyAsync(x => x.Id == entityId, cancellationToken),
            nameof(AdjustmentDocument) => _db.AdjustmentDocuments.AnyAsync(x => x.Id == entityId, cancellationToken),
            nameof(InventoryCheckDocument) => _db.InventoryCheckDocuments.AnyAsync(x => x.Id == entityId, cancellationToken),
            _ => Task.FromResult(false)
        };
    }
}
