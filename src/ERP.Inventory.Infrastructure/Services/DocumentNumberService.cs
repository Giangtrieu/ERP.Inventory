namespace ERP.Inventory.Infrastructure.Services;

public interface IDocumentNumberService
{
    string Next(string prefix, DateTime date);
}

public sealed class DocumentNumberService : IDocumentNumberService
{
    public string Next(string prefix, DateTime date)
    {
        var ticks = DateTime.Now.Ticks % 1000000;
        return $"{prefix}-{date:yyyy}-{ticks:000000}";
    }
}

