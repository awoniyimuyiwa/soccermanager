namespace Domain;

public interface IUnitOfWork
{
    /// <summary>
    /// Start an ambient transaction
    /// </summary>
    /// <param name="cancellationToken"></param>
    Task BeginTransaction(CancellationToken cancellationToken = default);

    /// <summary>
    ///  Persist changes to DB, commit and dispose previously started ambient transaction
    /// </summary>
    /// <param name="cancellationToken"></param>
    Task CommitTransaction(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rollback all changes within a previously started ambient transaction
    /// </summary>
    /// <param name="cancellationToken"></param>
    Task RollbackTransaction(CancellationToken cancellationToken = default);

    /// <summary>
    /// Persist changes to DB, the underlying persistence technology handles transaction handling
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task SaveChanges(CancellationToken cancellationToken = default);
}
