using Domain;
using EntityFrameworkCore.Extensions;
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

    public async Task<PlayerDto?> Get(
        Expression<Func<Player, bool>> expression,
        CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().Date);

        return await _context.Set<Player>() 
            .Where(expression)
            .ToInternalDto(today)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<PlayerDto>> GetAll(
        Expression<Func<Player, bool>> expression,
        CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().Date);

        return await _context.Set<Player>().Where(expression)
            .ToInternalDto(today)
            .ToListAsync(cancellationToken);
    }

    public Task<PaginatedList<PlayerDto>> Paginate(
        PlayerFilterDto? filter,
        int pageNumber = Domain.Constants.MinPageNumber,
        int pageSize = Domain.Constants.MaxPageSize,
        CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().Date);

        return _context.Set<Player>().ToPaginatedList<
            Player, 
            InternalPlayerDto,
            PlayerDto>(
            pageNumber,
            pageSize,
            q => q.ToInternalDto(today),
            p => p.CreatedAt,
            filter: q => filter != null ? ApplyFilter(filter) : q,
            cancellationToken);
    }

    public Task<CursorList<PlayerDto>> Stream(
        PlayerFilterDto? filter,
        Cursor? cursor,
        int pageSize = Domain.Constants.MaxPageSize,
        CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().Date);
 
        return _context.Set<Player>().ToCursorList<
            Player, 
            InternalPlayerDto, 
            PlayerDto>(
            cursor,
            pageSize,
            q => q.ToInternalDto(today),
            filter: q => filter != null ? ApplyFilter(filter) : q,
            cancellationToken);
    }

    private IQueryable<Player> ApplyFilter(PlayerFilterDto filter)
    {
        return _context.Set<Player>()        
            .WhereIf(filter.TeamId != null, p => p.Team.ExternalId == filter.TeamId)        
            .WhereIf(filter.OwnerId != null, p => p.Team.Owner.ExternalId == filter.OwnerId)        
            .WhereIf(!string.IsNullOrWhiteSpace(filter.SearchTerm),
             p => (p.FirstName != null && p.FirstName.Contains(filter.SearchTerm!))
                  || (p.LastName != null && p.LastName.Contains(filter.SearchTerm!)));
    }
}
