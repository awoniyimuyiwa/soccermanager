using Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace EntityFrameworkCore;

class UnitOfWork(ApplicationDbContext applicationDbContext) : IUnitOfWork, IDisposable
{
    readonly ApplicationDbContext _applicationDbContext = applicationDbContext;

    IDbContextTransaction? _currentTransaction;

    public async Task BeginTransaction(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction is not null) return;

        _currentTransaction = await _applicationDbContext.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransaction(CancellationToken cancellationToken = default)
    {
        try
        {
            await _applicationDbContext.SaveChangesAsync(cancellationToken);

            if (_currentTransaction is not null) await _currentTransaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            await RollbackTransaction(cancellationToken);
            HandleConcurrenyException(ex);
        }
        catch
        {
            await RollbackTransaction(cancellationToken);
            throw;
        }
        finally
        {
            DisposeTransaction();
        }
    }

    public async Task RollbackTransaction(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction is not null)
        {
            await _currentTransaction.RollbackAsync(cancellationToken);
            DisposeTransaction();
        }
    }

    public async Task SaveChanges(CancellationToken cancellationToken = default)
    {
        try
        {
           await _applicationDbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            HandleConcurrenyException(ex);
        }
        catch
        {
            throw;
        }
    }

    public void Dispose() => _applicationDbContext.Dispose();

    private static void HandleConcurrenyException(DbUpdateConcurrencyException ex)
    {
        var entry = ex.Entries[0];
        var entityName = entry.Entity.GetType().Name;
        var entityId = entry.Property("Id").CurrentValue;
        var databaseValues = entry.GetDatabaseValues();
        throw new ConcurrencyException(
            entityName,
            entityId ?? "",
            databaseValues?.ToObject() ?? "");
    }

    private void DisposeTransaction()
    {
        _currentTransaction?.Dispose();

        _currentTransaction = null;
    }
}
