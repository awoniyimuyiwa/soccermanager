using Domain;
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

    public Task<TeamDto?> Get(
        Expression<Func<Team, bool>> expression,
        CancellationToken cancellationToken = default)
    {
        return _context.Set<Team>().Where(expression)
            .Select(t => new TeamDto(
                t.ExternalId,
                t.Country,
                t.Name,
                t.Owner.FirstName,
                t.Owner.ExternalId,
                t.Owner.LastName,
                t.TransferBudget,
                t.Value,
                t.CreatedAt,
                t.UpdatedAt,
                t.ConcurrencyStamp))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PaginatedList<TeamDto>> Paginate(
        Guid? ownerId = null,
        string searchTerm = "",
        int pageNumber = Constants.MinPageNumber,
        int pageSize = Constants.MaxPageSize,
        CancellationToken cancellationToken = default)
    {
        pageNumber = Math.Max(Constants.MinPageNumber, pageNumber);
        pageSize = Math.Clamp(pageSize, Constants.MinPageSize, Constants.MaxPageSize);

        var query = _context.Set<Team>()
            .AsNoTracking()
            .WhereIf(ownerId != null, t => t.Owner.ExternalId == ownerId)
            .WhereIf(!string.IsNullOrWhiteSpace(searchTerm),
               team => team.Name != null && team.Name.Contains(searchTerm!));

        var count = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(tr => tr.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new TeamDto(
                t.ExternalId,
                t.Country,
                t.Name,
                t.Owner.FirstName,
                t.Owner.ExternalId,
                t.Owner.LastName,
                t.TransferBudget,
                t.Value,
                t.CreatedAt,
                t.UpdatedAt,
                t.ConcurrencyStamp))
            .ToListAsync(cancellationToken);

        return new PaginatedList<TeamDto>(
            items,
            count,
            pageNumber,
            pageSize);
    }
}
