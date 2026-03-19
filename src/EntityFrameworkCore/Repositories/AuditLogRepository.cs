using Domain;
using EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace EntityFrameworkCore.Repositories;

class AuditLogRepository(ApplicationDbContext context) : BaseRepository<AuditLog>(context), IAuditLogRepository
{
    public Task<FullAuditLogDto?> Get(
        Expression<Func<AuditLog, bool>> expression,
        CancellationToken cancellationToken = default)
    {
        return _context.Set<AuditLog>().Where(expression)
            .Select(al => new FullAuditLogDto(
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
                al.User.ExternalId,
                al.AuditLogActions.Select(ala => new AuditLogActionDto(
                    ala.ExecutionTime,
                    ala.MethodName, 
                    ala.Parameters, 
                    ala.ServiceName)).ToList(), 
                al.EntityChanges.Select(ec => new EntityChangeDto(
                    ec.EntityName, 
                    ec.NewValues,
                    ec.OldValues,
                    ec.Type)).ToList()))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PaginatedList<AuditLogDto>> Paginate(
        AuditLogFilterDto? filter,
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
            ? ApplyFilter(filter) : _context.Set<AuditLog>();

        var count = await query
            .Take(Domain.Constants.MaxRowsToSkip + 1)
            .CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(al => al.TimeStamp)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToInternalDto()
            .ToListAsync(cancellationToken);

        return new PaginatedList<AuditLogDto>(
            items,
            count,
            pageNumber,
            pageSize);
    }

    public async Task<CursorList<AuditLogDto>> Stream(
        AuditLogFilterDto? filter,
        Cursor? cursor,
        int pageSize = Domain.Constants.MaxPageSize,
        CancellationToken cancellationToken = default)
    {
        pageSize = Math.Clamp(
            pageSize,
            Domain.Constants.MinPageSize,
            Domain.Constants.MaxPageSize);

        var query = filter is not null
          ? ApplyFilter(filter) : _context.Set<AuditLog>();

        // Descending order, newest first
        var items = await query
           .OrderByDescending(al => al.TimeStamp) 
           .ThenByDescending(al => al.Id) 
           .WhereIf(cursor != null, al => al.TimeStamp < cursor!.LastCreatedAt || (al.TimeStamp == cursor.LastCreatedAt && al.Id < cursor!.LastId))
           .Take(pageSize)
           .ToInternalDto()
           .ToListAsync(cancellationToken);

        var last = items.LastOrDefault();
        var next = last != null
            ? new Cursor(last.InternalId, last.TimeStamp)
            : null;

        return new CursorList<AuditLogDto>(
            items,
            next?.ToJson(),
            pageSize);
    }

    private IQueryable<AuditLog> ApplyFilter(AuditLogFilterDto filter)
    {
        return _context.Set<AuditLog>()
            .WhereIf(filter.From != null, al => al.TimeStamp >= filter.From)
            .WhereIf(filter.HttpMethod != null, al => al.HttpMethod == filter.HttpMethod)
            .WhereIf(filter.IpAddress != null, al => al.IpAddress != null && al.IpAddress.Contains(filter.IpAddress!))
            .WhereIf(filter.IsSuccessful != null, al => (al.Exception == null) == filter.IsSuccessful)
            .WhereIf(filter.RequestId != null, al => al.RequestId != null && al.RequestId.Contains(filter.RequestId!))
            .WhereIf(filter.StatusCode != null, al => al.StatusCode == filter.StatusCode)
            .WhereIf(filter.To != null, al => al.TimeStamp <= filter.To)
            .WhereIf(filter.Url != null, al => al.Url != null && al.Url.Contains(filter.Url!))
            .WhereIf(filter.UserId != null, al => al.User != null && al.User.ExternalId == filter.UserId); ;
    }
}
