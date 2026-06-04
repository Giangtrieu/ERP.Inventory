namespace ERP.Inventory.Domain.Enums;

public enum SystemErrorCategory
{
    BusinessValidation,
    BusinessDependency,
    Timeout,
    Deadlock,
    Unauthorized,
    Forbidden,
    NotFound,
    DbUpdateException,
    UnhandledException
}
