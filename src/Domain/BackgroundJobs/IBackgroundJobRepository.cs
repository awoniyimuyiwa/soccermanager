using System.Linq.Expressions;

namespace Domain.BackgroundJobs;

public interface IBackgroundJobRepository : IRepository<BackgroundJob>
{
    Task<BackgroundJobDto?> Get(
        Expression<Func<BackgroundJob, bool>> expression,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<long>> GetIds(
        Expression<Func<BackgroundJob, bool>> expression,
        int batchSize,
        CancellationToken cancellationToken = default);

    Task<PaginatedList<BackgroundJobDto>> Paginate(
        GetBackgroundJobFilterDto? filter,
        int pageNumber = Constants.MinPageNumber,
        int pageSize = Constants.MaxPageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Requeues failed jobs.
    /// </summary>
    /// <param name="filter">The filtering criteria. If null or properties are empty, all failed jobs are processed.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>The number of jobs successfully requeued.</returns>/param>
    Task<int> RequeueFailed(
        RequeueBackgroundJobFilterDto? filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Requeues jobs that have been stuck in "In progress" state
    /// </summary>
    /// <param name="filter">The filtering criteria. If null or properties are empty, all stuck jobs are processed.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>The number of jobs successfully requeued.</returns>
    Task<int> RequeueStuck(
        RequeueBackgroundJobFilterDto? filter,
        CancellationToken cancellationToken = default);

    Task<CursorList<BackgroundJobDto>> Stream(
       GetBackgroundJobFilterDto? filter,
       PageCursor? cursor,
       int pageSize = Constants.MaxPageSize,
       CancellationToken cancellationToken = default);
}

