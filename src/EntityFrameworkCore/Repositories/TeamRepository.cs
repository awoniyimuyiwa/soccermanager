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

    public Task<PaginatedList<TeamDto>> Paginate(
        TeamFilterDto? filter,
        int pageNumber = Domain.Constants.MinPageNumber,
        int pageSize = Domain.Constants.MaxPageSize,
        CancellationToken cancellationToken = default) => 

        _context.Set<Team>()
        .ToPaginatedList<
            Team, 
            InternalTeamDto, 
            TeamDto>(
            pageNumber,
            pageSize,
            q => q.ToInternalDto(),
            t => t.CreatedAt,
            filter: q => filter != null ? ApplyFilter(filter) : q,
            cancellationToken);

    public Task<CursorList<TeamDto>> Stream(
        TeamFilterDto? filter,
        Cursor? cursor,
        int pageSize = Domain.Constants.MaxPageSize,
        CancellationToken cancellationToken = default) =>

        _context.Set<Team>()    
        .ToCursorList<
            Team, 
            InternalTeamDto, 
            TeamDto>(
            cursor,
            pageSize,
            q => q.ToInternalDto(),
            filter: q => filter != null ? ApplyFilter(filter) : q,
            cancellationToken);

    private IQueryable<Team> ApplyFilter(TeamFilterDto filter)
    {
        return _context.Set<Team>()
            .WhereIf(filter.OwnerId != null, t => t.Owner.ExternalId == filter.OwnerId)
            .WhereIf(!string.IsNullOrWhiteSpace(filter.SearchTerm),
            team => team.Name != null && team.Name.Contains(filter.SearchTerm!));
    }
}
