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
    /// Checks the change tracker to determine if any new <see cref="BackgroundJob"/> entities 
    /// are pending insertion in the current unit of work.
    /// </summary>
    /// <returns>True if at least one background job is marked as Added; otherwise, false.</returns>
    /// <remarks>
    /// This must be called before <see cref="CommitTransaction"/>, as committing resets the entity states.
    /// </remarks>
    bool HasBackgroundJobs();

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
