using Domain;
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
            al.TimeStamp,
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
}
