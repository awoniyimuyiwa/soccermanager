using Domain;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace EntityFrameworkCore.Repositories;

class PlayerRepository(
    ApplicationDbContext context,
    TimeProvider timeProvider) : BaseRepository<Player>(context), IPlayerRepository
{

    public void AddPlayerValue(PlayerValue playerValue)
    {
        _context.Set<PlayerValue>().Add(playerValue);
    }

    public Task<PlayerDto?> Get(
        Expression<Func<Player, bool>> expression,
        CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().Date);

        return _context.Set<Player>().Where(expression)
            .Select(p => new PlayerDto(
                p.Id,
                p.GetAge(today),
                p.Country,
                p.DateOfBirth,
                p.FirstName,
                p.LastName,
                p.TeamId,
                p.Type,
                p.Value,
                p.CreatedAt,
                p.UpdatedAt,
                p.ConcurrencyStamp))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PaginatedList<PlayerDto>> Paginate(
        Guid? teamId = null,
        Guid? ownerId = null,
        string searchTerm = "",
        int pageNumber = Constants.MinPageNumber,
        int pageSize = Constants.MaxPageSize,
        CancellationToken cancellationToken = default)
    {
        pageNumber = Math.Max(Constants.MinPageNumber, pageNumber);
        pageSize = Math.Clamp(pageSize, Constants.MinPageSize, Constants.MaxPageSize);

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().Date);

        var query = _context.Set<Player>()
            .AsNoTracking()
            .WhereIf(teamId != null, p => p.TeamId == teamId)
            .WhereIf(ownerId != null, p => p.Team.OwnerId == ownerId)
            .WhereIf(!string.IsNullOrWhiteSpace(searchTerm),
                p => (p.FirstName != null && p.FirstName.Contains(searchTerm!))
                     || (p.LastName != null && p.LastName.Contains(searchTerm!)));

        var count = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(tr => tr.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new PlayerDto(
                p.Id,
                p.GetAge(today),
                p.Country,
                p.DateOfBirth,
                p.FirstName,
                p.LastName,
                p.TeamId,
                p.Type,
                p.Value,
                p.CreatedAt,
                p.UpdatedAt,
                p.ConcurrencyStamp))
            .ToListAsync(cancellationToken);

        return new PaginatedList<PlayerDto>(
            items,
            count,
            pageNumber,
            pageSize);
    }
}
