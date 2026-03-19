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

    public async Task<PaginatedList<PlayerDto>> Paginate(
        PlayerFilterDto? filter,
        int pageNumber = Domain.Constants.MinPageNumber,
        int pageSize = Domain.Constants.MaxPageSize,
        CancellationToken cancellationToken = default)
    {
        pageSize = Math.Clamp(
            pageSize,
            Domain.Constants.MinPageSize,
            Domain.Constants.MaxPageSize);

        var maxPageNumber = (Domain.Constants.MaxRowsToSkip / pageSize) + 1;
        pageNumber = Math.Clamp(
            pageNumber,
            Domain.Constants.MinPageNumber,
            maxPageNumber);

        var query = filter is not null
            ? ApplyFilter(filter) : _context.Set<Player>();

        var count = await query
            .Take(Domain.Constants.MaxRowsToSkip + 1)
            .CountAsync(cancellationToken);

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().Date);

        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToInternalDto(today)
            .ToListAsync(cancellationToken);

        return new PaginatedList<PlayerDto>(
            items,
            count,
            pageNumber,
            pageSize);
    }

    public async Task<CursorList<PlayerDto>> Stream(
        PlayerFilterDto? filter,
        Cursor? cursor,
        int pageSize = Domain.Constants.MaxPageSize,
        CancellationToken cancellationToken = default)
    {
        pageSize = Math.Clamp(
            pageSize,
            Domain.Constants.MinPageSize,
            Domain.Constants.MaxPageSize);

        var query = filter is not null
            ? ApplyFilter(filter) : _context.Set<Player>();

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().Date);

        // Descending order, newest first
        var items = await query
           .OrderByDescending(p => p.CreatedAt)
           .ThenByDescending(p => p.Id)
           .WhereIf(cursor != null, p => p.CreatedAt < cursor!.LastCreatedAt || (p.CreatedAt == cursor.LastCreatedAt && p.Id < cursor!.LastId))
           .Take(pageSize)
           .ToInternalDto(today)
           .ToListAsync(cancellationToken);

        var last = items.LastOrDefault();
        var next = last != null
            ? new Cursor(last.InternalId, last.CreatedAt)
            : null;

        return new CursorList<PlayerDto>(
            items,
            next?.ToJson(),
            pageSize);
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
