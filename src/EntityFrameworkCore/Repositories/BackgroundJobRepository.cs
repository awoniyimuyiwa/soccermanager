using Domain;
using Domain.BackgroundJobs;
using EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace EntityFrameworkCore.Repositories;

class BackgroundJobRepository(
    ApplicationDbContext context,
    TimeProvider timeProvider) : BaseRepository<BackgroundJob>(context), IBackgroundJobRepository
{
    readonly TimeProvider _timeProvider = timeProvider;

    public async Task<BackgroundJobDto?> Get(
        Expression<Func<BackgroundJob, bool>> expression,
        CancellationToken cancellationToken = default)
    {
        return await( _context.Set<BackgroundJob>()
            .Where(expression)
            .ToInternalDto()
            .FirstOrDefaultAsync(cancellationToken));
    }

    public async Task<IReadOnlyCollection<long>> GetIds(
        Expression<Func<BackgroundJob, bool>> expression,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        return await _context.Set<BackgroundJob>()
            .Where(expression)
            // Highest priority first
            .OrderByDescending(bj => bj.Priority)
            // Oldest scheduled date second (FIFO within same priority)
            .ThenBy(bj => bj.ScheduledFor)
            .Select(bj => bj.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<PaginatedList<BackgroundJobDto>> Paginate(
        GetBackgroundJobFilterDto? filter,
        int pageNumber = Domain.Constants.MinPageNumber,
        int pageSize = Domain.Constants.MaxPageSize,
        CancellationToken cancellationToken = default)
    {
        pageSize = Math.Clamp(
            pageSize,
            Domain.Constants.MinPageSize,
            Domain.Constants.MaxPageSize);

        var maxPageNumber = (Domain.Constants.MaxRowsToSkip / pageSize) + 1;
        pageNumber = Math.Clamp(
            pageNumber,
            Domain.Constants.MinPageNumber,
            maxPageNumber);

        var query = filter is not null
            ? ApplyFilter(filter) : _context.Set<BackgroundJob>();

        var count = await query
            .Take(Domain.Constants.MaxRowsToSkip + 1)
            .CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(bj => bj.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToInternalDto()
            .ToListAsync(cancellationToken);

        return new PaginatedList<BackgroundJobDto>(
            items,
            count,
            pageNumber,
            pageSize);
    }

    public async Task<CursorList<BackgroundJobDto>> Stream(
        GetBackgroundJobFilterDto? filter,
        PageCursor? cursor,
        int pageSize = Domain.Constants.MaxPageSize,
        CancellationToken cancellationToken = default)
    {
        pageSize = Math.Clamp(
            pageSize,
            Domain.Constants.MinPageSize,
            Domain.Constants.MaxPageSize);

        var query = filter is not null
          ? ApplyFilter(filter) : _context.Set<BackgroundJob>();

        // Descending order, newest first
        var items = await query
           .OrderByDescending(bj => bj.CreatedAt) // OrderBy for ascending
           .ThenByDescending(bj => bj.Id) // Tie breaker must be unique // ThenBy for ascending 
           .WhereIf(cursor != null, bj => bj.CreatedAt < cursor!.LastCreatedAt || (bj.CreatedAt == cursor.LastCreatedAt && bj.Id < cursor!.LastId))
           // ascending: bj => bj.CreatedAt > cursor!.LastCreatedAt || (bj.CreatedAt == cursor.LastCreatedAt && bj.Id > cursor!.LastId))
           .Take(pageSize)
           .ToInternalDto()
           .ToListAsync(cancellationToken);

        var last = items.LastOrDefault();
        cursor = last != null
            ? new PageCursor(last.InternalId, last.CreatedAt)
            : null;

        return new CursorList<BackgroundJobDto>(
            items,
            cursor,
            pageSize);
    }

    public Task<int> RequeueFailed(
        RequeueBackgroundJobFilterDto? filter,
        CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();

        var query = filter is not null
         ? ApplyFilter(filter) : _context.Set<BackgroundJob>();

        return _context.Set<BackgroundJob>()
             .Where(bj => bj.Status == BackgroundJobStatus.Failed)
             .ExecuteUpdateAsync(s => s
             .SetProperty(bj => bj.Status, BackgroundJobStatus.Queued)
             .SetProperty(bj => bj.Attempts, (uint)0)
             .SetProperty(bj => bj.ScheduledFor, now)
             .SetProperty(bj => bj.Error, (string?)null)
             // Invalidate concurrency stamps for all affected rows to prevent conflicts with zombie processes.
             .SetProperty(bj => bj.ConcurrencyStamp, _ => Guid.NewGuid().ToString()),
             cancellationToken);
    }

    public Task<int> RequeueStuck(
        RequeueBackgroundJobFilterDto? filter,
        CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();

        var query = filter is not null
            ? ApplyFilter(filter) : _context.Set<BackgroundJob>();

        return query
            .Where(bj => bj.Status == BackgroundJobStatus.InProgress)
            .ExecuteUpdateAsync(s => s
            .SetProperty(bj => bj.Status, BackgroundJobStatus.Queued)
            .SetProperty(bj => bj.Error, "Reset by automated cleanup.")
            .SetProperty(bj => bj.UpdatedAt, now)
            // Invalidate concurrency stamps for all affected rows to prevent conflicts with zombie processes.
            .SetProperty(bj => bj.ConcurrencyStamp, _ => Guid.NewGuid().ToString()),
            cancellationToken);
    }

    private IQueryable<BackgroundJob> ApplyFilter(RequeueBackgroundJobFilterDto filter)
    {
        return ApplyFilter((BackgroundJobFilterDto)filter)
            .WhereIf(filter.Ids != null && filter.Ids.Length > 0, bj => filter.Ids!.Contains(bj.ExternalId))
            .WhereIf(filter.SourceIds != null && filter.SourceIds.Length > 0, bj => bj.SourceId != null && filter.SourceIds!.Contains(bj.SourceId.Value))
            .WhereIf(filter.TraceIds != null && filter.TraceIds.Length > 0, bj => bj.TraceId != null && filter.TraceIds!.Contains(bj.TraceId));
    }

    private IQueryable<BackgroundJob> ApplyFilter(GetBackgroundJobFilterDto filter)
    {
        return ApplyFilter((BackgroundJobFilterDto)filter)
            .WhereIf(filter.Statuses != null && filter.Statuses.Length > 0, bj => filter.Statuses!.Contains(bj.Status));
    }

    private IQueryable<BackgroundJob> ApplyFilter(BackgroundJobFilterDto filter)
    {
        return _context.Set<BackgroundJob>()
            .WhereIf(filter.CreatedFrom != null, bj => bj.CreatedAt >= filter!.CreatedFrom)
            .WhereIf(filter.CreatedTo != null, bj => bj.CreatedAt <= filter!.CreatedTo)
            .WhereIf(filter.Priorities != null && filter.Priorities.Length > 0, bj => filter.Priorities!.Contains(bj.Priority))
            .WhereIf(filter.ScheduledFrom != null, bj => bj.ScheduledFor >= filter!.ScheduledFrom)
            .WhereIf(filter.ScheduledTo != null, bj => bj.ScheduledFor <= filter!.ScheduledTo)
            .WhereIf(filter.Types != null && filter.Types.Length > 0, bj => filter.Types!.Contains(bj.Type))
            .WhereIf(filter.UpdatedFrom != null, bj => bj.UpdatedAt >= filter!.UpdatedFrom)
            .WhereIf(filter.UpdatedTo != null, bj => bj.UpdatedAt <= filter!.UpdatedTo);
    }
}
