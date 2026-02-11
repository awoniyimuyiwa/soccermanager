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
        DateTimeOffset? from = null,
        string? httpMethod = null,
        string? ipAddress = null,
        bool? isSuccessful = null,
        string? requestId = null,
        int? statusCode = null,
        DateTimeOffset? to = null,
        string? url = null,
        Guid? userId = null,
        int pageNumber = Domain.Constants.MinPageNumber,
        int pageSize = Domain.Constants.MaxPageSize,
        CancellationToken cancellationToken = default)
    {
        pageNumber = Math.Max(Domain.Constants.MinPageNumber, pageNumber);
        pageSize = Math.Clamp(
            pageSize,
            Domain.Constants.MinPageSize,
            Domain.Constants.MaxPageSize);

        var query = _context.Set<AuditLog>()
            .WhereIf(from != null, al => al.TimeStamp >= from)
            .WhereIf(httpMethod != null, al => al.HttpMethod == httpMethod)
            .WhereIf(ipAddress != null, al => al.IpAddress != null && al.IpAddress.Contains(ipAddress!))
            .WhereIf(isSuccessful != null, al => (al.Exception == null) == isSuccessful)
            .WhereIf(requestId != null, al => al.RequestId != null && al.RequestId.Contains(requestId!))
            .WhereIf(statusCode != null, al => al.StatusCode == statusCode)
            .WhereIf(to != null, al => al.TimeStamp <= to)
            .WhereIf(url != null, al => al.Url != null && al.Url.Contains(url!))
            .WhereIf(userId != null, al => al.User != null && al.User.ExternalId == userId);
           
        var count = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(al => al.TimeStamp)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(al => new AuditLogDto(
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
                al.User.ExternalId))
            .ToListAsync(cancellationToken);

        return new PaginatedList<AuditLogDto>(
            items,
            count,
            pageNumber,
            pageSize);
    }
}
