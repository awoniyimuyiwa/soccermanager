
using System.Linq.Expressions;

namespace Domain;

public interface ITransferRepository : IRepository<Transfer>
{
    public Task<FullTransferDto?> FindAsFullDto(
        Expression<Func<Transfer, bool>> expression,
        CancellationToken cancellationToken = default);

    Task<PaginatedList<FullTransferDto>> Paginate(
        bool? isPending = null,
        Guid? ownerId = null, 
        string searchTerm = "",
        int pageNumber = Constants.MinPageNumber,
        int pageSize = Constants.MaxPageSize,
        CancellationToken cancellationToken = default);
}