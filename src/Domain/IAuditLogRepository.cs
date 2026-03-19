
using System.Linq.Expressions;

namespace Domain;

public interface IAuditLogRepository : IRepository<AuditLog>
{
    Task<FullAuditLogDto?> Get(
        Expression<Func<AuditLog, bool>> expression,
        CancellationToken cancellationToken = default);

    Task<PaginatedList<AuditLogDto>> Paginate(
        AuditLogFilterDto? filter,
        int pageNumber = Constants.MinPageNumber,
        int pageSize = Constants.MaxPageSize,
        CancellationToken cancellationToken = default);

    Task<CursorList<AuditLogDto>> Stream(
        AuditLogFilterDto? filter, 
        Cursor? cursor, 
        int pageSize = Constants.MaxPageSize, 
        CancellationToken cancellationToken = default);
}
