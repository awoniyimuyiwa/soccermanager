
using System.Linq.Expressions;

namespace Domain;

public interface ITeamRepository : IBaseRepository<Team>
{
    void AddTransferBudgetValue(
        TransferBudgetValue transferBudgetValue);

    Task<bool> Any(
        Expression<Func<Team, bool>> expression,
        CancellationToken cancellationToken = default);

    Task<TeamDto?> Get(
        Expression<Func<Team, bool>> expression,
        CancellationToken cancellationToken = default);

    Task<PaginatedList<TeamDto>> Paginate(
        Guid? ownerId = null,
        string searchTerm = "",
        int pageNumber = Constants.MinPageNumber,
        int pageSize = Constants.MaxPageSize,
        CancellationToken cancellationToken = default);
}
