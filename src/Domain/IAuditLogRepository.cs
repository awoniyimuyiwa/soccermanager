
using System.Linq.Expressions;

namespace Domain;

public interface IAuditLogRepository : IRepository<AuditLog>
{
    Task<FullAuditLogDto?> Get(
        Expression<Func<AuditLog, bool>> expression,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get audit logs
    /// </summary>
    /// <param name="from">Minimum timestamp. (Value is inclusive)</param>
    /// <param name="httpMethod"></param>
    /// <param name="ipAddress"></param>
    /// <param name="isSuccessful"></param>
    /// <param name="requestId"></param>
    /// <param name="statusCode"></param>
    /// <param name="to">Maximum timestamp. (Value is inclusive)</param>
    /// <param name="url"></param>
    /// <param name="userId"></param>
    /// <param name="pageNumber"></param>
    /// <param name="pageSize"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<PaginatedList<AuditLogDto>> Paginate(
        DateTimeOffset? from = null,
        string? httpMethod = null,
        string? ipAddress = null,
        bool? isSuccessful = null,
        string? requestId = null,
        int? statusCode = null,
        DateTimeOffset? to = null,
        string? url = null,
        Guid? userId = null,
        int pageNumber = Constants.MinPageNumber,
        int pageSize = Constants.MaxPageSize,
        CancellationToken cancellationToken = default);
}
