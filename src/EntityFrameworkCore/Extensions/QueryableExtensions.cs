using Domain.BackgroundJobs;
using System.Linq.Expressions;

namespace EntityFrameworkCore.Extensions;

static class QueryableExtensions
{
    public static IQueryable<T> WhereIf<T>(
        this IQueryable<T> query,
        bool condition,
        Expression<Func<T, bool>> predicate)
    {
        return condition ? query.Where(predicate) : query;
    }

    public static IQueryable<InternalBackgroundJobDto> ToInternalDto(this IQueryable<BackgroundJob> query)
    {
        return query.Select(bj => new InternalBackgroundJobDto(
            bj.Id,
            bj.ExternalId,    
            bj.Attempts,    
            bj.Error,
            bj.MaxRetries,
            bj.Payload,
            bj.Priority,
            bj.ScheduledFor,
            bj.SourceId,
            bj.Status,
            bj.TraceId,
            bj.Type,
            bj.CreatedAt,
            bj.UpdatedAt,
            bj.ConcurrencyStamp));
    }
}
