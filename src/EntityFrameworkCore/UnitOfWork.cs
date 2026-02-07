using Domain;
using Microsoft.Data.SqlClient;
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
            throw;
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sqlEx)
        {
            await RollbackTransaction(cancellationToken);
            HandleTriggerAndCheckConstraintError(sqlEx);
            throw;
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
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sqlEx)
        {
            HandleTriggerAndCheckConstraintError(sqlEx);
        }
        // Any other exception (like a Timeout or Network issue) naturally bubbles up from here.
    }

    public void Dispose() => _applicationDbContext.Dispose();

    private static void HandleTriggerAndCheckConstraintError(SqlException sqlEx)
    {
        var isTransferBudgetTriggerError = sqlEx.Number == 50000 &&
            sqlEx.Message.Contains(Domain.Constants.InsufficientTeamTransferBudgetErrorMessage);

        bool isTransferBudgetConstraintError = sqlEx.Number == 547 &&
            sqlEx.Message.Contains(Constants.TeamTransferBudgetCheckConstraintName);

        if (isTransferBudgetTriggerError || isTransferBudgetConstraintError)
        {
            throw new DomainException(Domain.Constants.InsufficientTeamTransferBudgetErrorMessage);
        }
    }

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
