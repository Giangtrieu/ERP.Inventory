using ERP.Inventory.Domain.Enums;

namespace ERP.Inventory.Domain.Common;

public abstract class DocumentBase : AuditableEntity
{
    public string DocumentNo { get; set; } = string.Empty;
    public DateTime DocumentDate { get; set; } = DateTime.Now;
    public DocumentStatus Status { get; set; } = DocumentStatus.Posted;
    public string? Note { get; set; }
    public string ApprovedBy { get; set; } = string.Empty;
    public DateTime ApprovedAt { get; set; } = DateTime.Now;
    public DateTime PostedAt { get; set; } = DateTime.Now;
}

