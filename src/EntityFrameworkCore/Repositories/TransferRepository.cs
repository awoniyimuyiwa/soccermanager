using Domain;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace EntityFrameworkCore.Repositories;

class TransferRepository(ApplicationDbContext context) : BaseRepository<Transfer>(context), ITransferRepository
{
    public Task<FullTransferDto?> FindAsFullDto(
        Expression<Func<Transfer, bool>> expression,
        CancellationToken cancellationToken = default)
    {
        return _context.Set<Transfer>()
           .Where(expression)
           .Select(tr => new FullTransferDto(   
               tr.ExternalId,
               tr.AskingPrice,   
               tr.FromTeam.ExternalId,    
               tr.FromTeam.Name,    
               tr.Player.FirstName,  
               tr.Player.ExternalId,    
               tr.Player.LastName,    
               tr.ToTeam != null ? tr.ToTeam.ExternalId : null,
               tr.ToTeam != null ? tr.ToTeam.Name : null,    
               tr.CreatedAt,    
               tr.UpdatedAt,    
               tr.ConcurrencyStamp))
           .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PaginatedList<FullTransferDto>> Paginate(
        bool? isPending = null,
        Guid? ownerId = null,
        string searchTerm = "",
        int pageNumber = 1,
        int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        pageNumber = Math.Max(Constants.MinPageNumber, pageNumber);
        pageSize = Math.Clamp(pageSize, Constants.MinPageSize, Constants.MaxPageSize);

        var query = _context.Set<Transfer>()
            .AsNoTracking()
            .WhereIf(isPending == true, tr => tr.ToTeamId == null)
            .WhereIf(isPending == false, tr => tr.ToTeamId != null)
            .WhereIf(ownerId != null, tr => tr.FromTeam.Owner.ExternalId == ownerId)
            .WhereIf(!string.IsNullOrWhiteSpace(searchTerm), tr =>
            tr.Player.FirstName!.Contains(searchTerm) ||
            tr.Player.LastName!.Contains(searchTerm));

        var count = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(tr => tr.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(tr => new FullTransferDto(
                tr.ExternalId,
                tr.AskingPrice,
                tr.FromTeam.ExternalId,
                tr.FromTeam.Name,
                tr.Player.FirstName,
                tr.Player.ExternalId,
                tr.Player.LastName,
                tr.ToTeam != null ? tr.ToTeam.ExternalId : null,
                tr.ToTeam != null ? tr.ToTeam.Name : null,
                tr.CreatedAt,
                tr.UpdatedAt,
                tr.ConcurrencyStamp))
            .ToListAsync(cancellationToken);

        return new PaginatedList<FullTransferDto>(
            items, 
            count, 
            pageNumber, 
            pageSize);
    }
}
