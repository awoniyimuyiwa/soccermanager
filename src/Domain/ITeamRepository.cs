
using System.Linq.Expressions;

namespace Domain;

public interface ITeamRepository : IRepository<Team>
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
        TeamFilterDto? filter,
        int pageNumber = Constants.MinPageNumber,
        int pageSize = Constants.MaxPageSize,
        CancellationToken cancellationToken = default);

    Task<CursorList<TeamDto>> Stream(
        TeamFilterDto? filter, 
        Cursor? cursor,
        int pageSize = Constants.MaxPageSize,
        CancellationToken cancellationToken = default);
}
