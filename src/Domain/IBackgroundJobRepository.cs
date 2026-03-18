
using System.Linq.Expressions;

namespace Domain;

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
        BackgroundJobFilterDto? filter,
        int pageNumber = Constants.MinPageNumber,
        int pageSize = Constants.MaxPageSize,
        CancellationToken cancellationToken = default);

    Task<int> RequeueFailed(
        Guid[] ids,
        CancellationToken cancellationToken = default);

    Task<int> RequeueStuck(
        Guid[] ids,
        uint afterMinutes,
        CancellationToken cancellationToken = default);

    Task<CursorList<BackgroundJobDto>> Stream(
       BackgroundJobFilterDto? filter,
       PageCursor? cursor,
       int pageSize = Constants.MaxPageSize,
       CancellationToken cancellationToken = default);
}

