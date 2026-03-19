
using System.Linq.Expressions;

namespace Domain;

public interface ITransferRepository : IRepository<Transfer>
{
    public Task<FullTransferDto?> FindAsFullDto(
        Expression<Func<Transfer, bool>> expression,
        CancellationToken cancellationToken = default);

    Task<PaginatedList<FullTransferDto>> Paginate(
        TransferFilterDto filter,
        int pageNumber = Constants.MinPageNumber,
        int pageSize = Constants.MaxPageSize,
        CancellationToken cancellationToken = default);

    Task<CursorList<FullTransferDto>> Stream(
        TransferFilterDto? filter, 
        Cursor? cursor,
        int pageSize = Constants.MaxPageSize,
        CancellationToken cancellationToken = default);
}