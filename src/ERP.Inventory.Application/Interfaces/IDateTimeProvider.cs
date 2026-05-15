namespace ERP.Inventory.Application.Interfaces;

/// <summary>
/// Provides a consistent, testable source of current UTC time throughout the application.
/// Replaces all direct usage of DateTime.UtcNow / DateTime.UtcNow.
/// </summary>
public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}
