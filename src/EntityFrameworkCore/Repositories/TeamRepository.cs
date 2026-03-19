using Domain;
using EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace EntityFrameworkCore.Repositories;

class TeamRepository(ApplicationDbContext context) : BaseRepository<Team>(context), ITeamRepository
{
    public Task<bool> Any(
        Expression<Func<Team, bool>> expression,
        CancellationToken cancellationToken = default) =>
        _context.Set<Team>().AnyAsync(expression, cancellationToken);
          
    public void AddTransferBudgetValue(TransferBudgetValue transferBudgetValue)
    {
        _context.Set<TransferBudgetValue>().Add(transferBudgetValue);
    }

    public async Task<TeamDto?> Get(
        Expression<Func<Team, bool>> expression,
        CancellationToken cancellationToken = default)
    {
        return await _context.Set<Team>()
            .Where(expression)
            .ToInternalDto()
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PaginatedList<TeamDto>> Paginate(
        TeamFilterDto? filter,
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
            ? ApplyFilter(filter) : _context.Set<Team>();

        var count = await query
            .Take(Domain.Constants.MaxRowsToSkip + 1)
            .CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToInternalDto()
            .ToListAsync(cancellationToken);

        return new PaginatedList<TeamDto>(
            items,
            count,
            pageNumber,
            pageSize);
    }

    public async Task<CursorList<TeamDto>> Stream(
        TeamFilterDto? filter,
        Cursor? cursor,
        int pageSize = Domain.Constants.MaxPageSize,
        CancellationToken cancellationToken = default)
    {
        pageSize = Math.Clamp(
            pageSize,
            Domain.Constants.MinPageSize,
            Domain.Constants.MaxPageSize);

        var query = filter is not null
            ? ApplyFilter(filter) : _context.Set<Team>();

        // Descending order, newest first
        var items = await query
           .OrderByDescending(t => t.CreatedAt)
           .ThenByDescending(t => t.Id)
           .WhereIf(cursor != null, t => t.CreatedAt < cursor!.LastCreatedAt || (t.CreatedAt == cursor.LastCreatedAt && t.Id < cursor!.LastId))
           .Take(pageSize)
           .ToInternalDto()
           .ToListAsync(cancellationToken);

        var last = items.LastOrDefault();
        var next = last != null
            ? new Cursor(last.InternalId, last.CreatedAt)
            : null;

        return new CursorList<TeamDto>(
            items,
            next?.ToJson(),
            pageSize);
    }

    private IQueryable<Team> ApplyFilter(TeamFilterDto filter)
    {
        return _context.Set<Team>()
            .WhereIf(filter.OwnerId != null, t => t.Owner.ExternalId == filter.OwnerId)
            .WhereIf(!string.IsNullOrWhiteSpace(filter.SearchTerm),
            team => team.Name != null && team.Name.Contains(filter.SearchTerm!));
    }
}
