using System.Linq.Expressions;
using ERP.Inventory.Application.Interfaces;
using ERP.Inventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ERP.Inventory.Infrastructure.Services;

/// <summary>
/// Generic repository implementation backed by EF Core.
/// </summary>
public sealed class EfRepository<T> : IRepository<T> where T : class
{
    private readonly InventoryDbContext _db;
    private readonly DbSet<T> _set;

    public EfRepository(InventoryDbContext db)
    {
        _db = db;
        _set = db.Set<T>();
    }

    public IQueryable<T> Query() => _set.AsQueryable();
    public IQueryable<T> QueryNoTracking() => _set.AsNoTracking();

    public async Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => await _set.FindAsync(new object[] { id }, cancellationToken);

    public async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        => await _set.FirstOrDefaultAsync(predicate, cancellationToken);

    public async Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        => await _set.AnyAsync(predicate, cancellationToken);

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
        => await _set.CountAsync(cancellationToken);

    public async Task<int> CountAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        => await _set.CountAsync(predicate, cancellationToken);

    public void Add(T entity) => _set.Add(entity);
    public void AddRange(IEnumerable<T> entities) => _set.AddRange(entities);
    public void Remove(T entity) => _set.Remove(entity);
    public void RemoveRange(IEnumerable<T> entities) => _set.RemoveRange(entities);
}

/// <summary>
/// Unit of Work implementation wrapping InventoryDbContext with transaction support.
/// </summary>
public sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly InventoryDbContext _db;
    private IDbContextTransaction? _transaction;
    private readonly Dictionary<Type, object> _repos = new();
    private bool _disposed;

    public EfUnitOfWork(InventoryDbContext db)
    {
        _db = db;
    }

    public IRepository<T> Repository<T>() where T : class
    {
        var type = typeof(T);
        if (!_repos.TryGetValue(type, out var repo))
        {
            repo = new EfRepository<T>(_db);
            _repos[type] = repo;
        }
        return (IRepository<T>)repo;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _db.SaveChangesAsync(cancellationToken);

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        _transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            await _transaction.CommitAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _transaction?.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Default IDateTimeProvider returning DateTime.UtcNow.
/// </summary>
public sealed class UtcDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
