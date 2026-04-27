using ERP.Inventory.Domain.Common;
using ERP.Inventory.Domain.Enums;

namespace ERP.Inventory.Domain.Entities;

public class SystemUser
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string UserName { get; set; } = string.Empty;
    public string NormalizedUserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string PreferredLanguage { get; set; } = "vi";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public ICollection<SystemUserRole> UserRoles { get; set; } = new List<SystemUserRole>();
}

public class SystemRole
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ICollection<SystemUserRole> UserRoles { get; set; } = new List<SystemUserRole>();
}

public class SystemUserRole
{
    public string UserId { get; set; } = string.Empty;
    public SystemUser? User { get; set; }
    public int RoleId { get; set; }
    public SystemRole? Role { get; set; }
}

public class ImportBatch : AuditableEntity
{
    public string BatchNo { get; set; } = string.Empty;
    public string ImportType { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public ImportBatchStatus Status { get; set; } = ImportBatchStatus.Uploaded;
    public int TotalRows { get; set; }
    public int BlockingErrorRows { get; set; }
    public ICollection<ImportBatchRow> Rows { get; set; } = new List<ImportBatchRow>();
}

public class ImportBatchRow : AuditableEntity
{
    public int ImportBatchId { get; set; }
    public ImportBatch? ImportBatch { get; set; }
    public int RowNumber { get; set; }
    public string RawJson { get; set; } = "{}";
    public string? ColumnName { get; set; }
    public ValidationSeverity Severity { get; set; } = ValidationSeverity.Info;
    public string? Message { get; set; }
    public string? SuggestedFix { get; set; }
    public bool IsValid { get; set; } = true;
}

public class Attachment : AuditableEntity
{
    public string EntityName { get; set; } = string.Empty;
    public int EntityId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
}

public class AuditLog
{
    public long Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public int? EntityId { get; set; }
    public string? ReferenceNo { get; set; }
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public string Result { get; set; } = "Success";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class Translation : AuditableEntity
{
    public string EntityName { get; set; } = string.Empty;
    public int EntityId { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string LanguageCode { get; set; } = "vi";
    public string Value { get; set; } = string.Empty;
}

public class Notification : AuditableEntity
{
    public string UserId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? LinkUrl { get; set; }
    public bool IsRead { get; set; }
}

public class UserWarehousePermission : AuditableEntity
{
    public string UserId { get; set; } = string.Empty;
    public int WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public bool CanView { get; set; } = true;
    public bool CanOperate { get; set; }
    public bool CanManage { get; set; }
}
