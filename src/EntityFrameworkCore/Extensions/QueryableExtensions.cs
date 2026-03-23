using Domain;
using Domain.BackgroundJobs;
using Microsoft.EntityFrameworkCore;
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

    public static async Task<CursorList<TResultDto>> ToCursorList<TEntity, TInternalDto, TResultDto>(
        this IQueryable<TEntity> query,
        Cursor? cursor,
        int pageSize,
        Func<IQueryable<TEntity>, IQueryable<TInternalDto>> projector,
        Func<IQueryable<TEntity>, IQueryable<TEntity>>? filter = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IHasCursorMetadata
        where TInternalDto : class, IHasInternalCursor, TResultDto
        where TResultDto: class
    {
        if (filter != null)
        {
            query = filter(query);
        }

        pageSize = Math.Clamp(
            pageSize, 
            Domain.Constants.MinPageSize,
            Domain.Constants.MaxPageSize);

        // Filter and Page on the DB side
        var items = await projector(query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .WhereIf(cursor != null, 
            x => x.CreatedAt < cursor!.LastCreatedAt
                 || (x.CreatedAt == cursor.LastCreatedAt && x.Id < cursor!.LastId))
            .Take(pageSize))
            .ToListAsync(cancellationToken);

        // Generate the next cursor from the last item
        var last = items.LastOrDefault();
        var next = last != null
            ? new Cursor(last.InternalId, last.CreatedAt)
            : null;

        // Return as the public DTO type (TResult)
        return new CursorList<TResultDto>(
            [.. items.Cast<TResultDto>()],
            next?.ToJson(),
            pageSize);
    }

    public static async Task<PaginatedList<TResultDto>> ToPaginatedList<TEntity, TInternalDto, TResultDto>(
        this IQueryable<TEntity> query,
        int pageNumber,
        int pageSize,
        Func<IQueryable<TEntity>, IQueryable<TInternalDto>> projector,
        Expression<Func<TEntity, object>> orderBy,
        Func<IQueryable<TEntity>, IQueryable<TEntity>>? filter = null,
        CancellationToken cancellationToken = default)
        where TEntity : class
        where TInternalDto : class, TResultDto // TInternalDto must inherit from TResultDto
        where TResultDto : class
    {
        if (filter != null)
        {
            query = filter(query);
        }

        pageSize = Math.Clamp(
            pageSize, 
            Domain.Constants.MinPageSize,
            Domain.Constants.MaxPageSize);

        int maxPageNumber = (Domain.Constants.MaxRowsToSkip / pageSize) + 1;
        pageNumber = Math.Clamp(
            pageNumber, 
            Domain.Constants.MinPageNumber,
            maxPageNumber);

        var count = await query
            .Take(Domain.Constants.MaxRowsToSkip + 1)
            .CountAsync(cancellationToken);

        // Apply sorting and paging on the Entity, then project
        var items = await projector(query
            .OrderByDescending(orderBy)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize))
            .ToListAsync(cancellationToken);

        // Cast the list of internal records to the public record type
        return new PaginatedList<TResultDto>(
            [.. items.Cast<TResultDto>()],
            count,
            pageNumber,
            pageSize);
    }

    public static IQueryable<InternalAuditLogDto> ToInternalDto(this IQueryable<AuditLog> query)
    {
        return query.Select(al => new InternalAuditLogDto(
            al.Id,
            al.ExternalId,
            al.BrowserInfo,
            al.Duration,
            al.Exception,
            al.HttpMethod,
            al.IpAddress,
            al.RequestId,
            al.StatusCode,
            al.CreatedAt,
            al.Url,
            al.User.ExternalId));
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

    public static IQueryable<InternalPlayerDto> ToInternalDto(this IQueryable<Player> query, DateOnly today)
    {
        return query.Select(p => new InternalPlayerDto(
            p.Id,
            p.ExternalId,
            today.Year - p.DateOfBirth.Year - (today < p.DateOfBirth.AddYears(today.Year - p.DateOfBirth.Year) ? 1 : 0),
            p.Country,
            p.DateOfBirth,
            p.FirstName,
            p.LastName,
            p.Team.ExternalId,
            p.Team.Name,
            p.Type,
            p.Value,
            p.CreatedAt,
            p.UpdatedAt,
            p.ConcurrencyStamp));
    }

    public static IQueryable<InternalTeamDto> ToInternalDto(this IQueryable<Team> query)
    {
        return query.Select(t => new InternalTeamDto(
            t.Id,
            t.ExternalId,
            t.Country,
            t.Name,
            t.Owner.FirstName,
            t.Owner.LastName,
            t.TransferBudget,
            t.Value,
            t.CreatedAt,
            t.UpdatedAt,
            t.ConcurrencyStamp
        ));
    }

    public static IQueryable<InternalFullTransferDto> ToInternalDto(this IQueryable<Transfer> query)
    {
        return query.Select(tr => new InternalFullTransferDto(
            tr.Id,
            tr.ExternalId,
            tr.AskingPrice,
            tr.FromTeam.ExternalId,
            tr.FromTeam.Name,
            tr.Player.FirstName,
            tr.Player.ExternalId,
            tr.Player.LastName,
            tr.ToTeam != null ? tr.ToTeam.ExternalId : null,
            tr.ToTeam != null ? tr.ToTeam.Name : null,
            tr.CreatedAt,
            tr.UpdatedAt,
            tr.ConcurrencyStamp));
    }

    public static IQueryable<UserDto> ToDto(this IQueryable<ApplicationUser> query)
    {
        return query.Select(u => new UserDto(
            u.Email,
            //u.ExternalId,
            u.FirstName,
            u.Id,
            u.EmailConfirmed,
            u.LastName,
            u.LockoutEnd,
            u.UserName,
            u.CreatedAt,
            u.UpdatedAt,
            u.ConcurrencyStamp));
    }
}
