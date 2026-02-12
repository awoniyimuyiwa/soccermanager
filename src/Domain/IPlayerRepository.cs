
using System.Linq.Expressions;

namespace Domain;

public interface IPlayerRepository : IRepository<Player>
{
    void AddPlayerValue(PlayerValue playerValue);

    Task<PlayerDto?> Get(
        Expression<Func<Player, bool>> expression,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<PlayerDto>> GetAll(
        Expression<Func<Player, bool>> expression,
        CancellationToken cancellationToken = default);

    Task<PaginatedList<PlayerDto>> Paginate(
        Guid? teamId = null,
        Guid? ownerId = null, 
        string searchTerm = "", 
        int pageNumber = Constants.MinPageNumber, 
        int pageSize = Constants.MaxPageSize, 
        CancellationToken cancellationToken = default);
}
