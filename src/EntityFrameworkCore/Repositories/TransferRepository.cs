using Domain;
using EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace EntityFrameworkCore.Repositories;

class TransferRepository(ApplicationDbContext context) : BaseRepository<Transfer>(context), ITransferRepository
{
    public async Task<FullTransferDto?> FindAsFullDto(
        Expression<Func<Transfer, bool>> expression,
        CancellationToken cancellationToken = default)
    {
        return await _context.Set<Transfer>()
           .Where(expression)
           .ToInternalDto()
           .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<PaginatedList<FullTransferDto>> Paginate(
        TransferFilterDto filter,
        int pageNumber = 1,
        int pageSize = Domain.Constants.MaxPageSize,
        CancellationToken cancellationToken = default) =>

        _context.Set<Transfer>()
        .ToPaginatedList<
            Transfer, 
            InternalFullTransferDto, 
            FullTransferDto>(
            pageNumber,
            pageSize,
            q => q.ToInternalDto(),
            tr => tr.CreatedAt,
            filter: q => filter != null ? ApplyFilter(filter) : q,
            cancellationToken);

    public Task<CursorList<FullTransferDto>> Stream(
        TransferFilterDto? filter,
        Cursor? cursor,
        int pageSize = Domain.Constants.MaxPageSize,
        CancellationToken cancellationToken = default) => 

        _context.Set<Transfer>()    
        .ToCursorList<
            Transfer, 
            InternalFullTransferDto, 
            FullTransferDto>(
            cursor,
            pageSize,
            q => q.ToInternalDto(),
            filter: q => filter != null ? ApplyFilter(filter) : q,
            cancellationToken);

    private IQueryable<Transfer> ApplyFilter(TransferFilterDto filter)
    {
        return _context.Set<Transfer>()
            .WhereIf(filter.IsPending == true, tr => tr.ToTeamId == null)
            .WhereIf(filter.IsPending == false, tr => tr.ToTeamId != null)
            .WhereIf(filter.OwnerId != null, tr => tr.FromTeam.Owner.ExternalId == filter.OwnerId)
            .WhereIf(!string.IsNullOrWhiteSpace(filter.Search), tr =>
            tr.Player.FirstName!.Contains(filter!.Search!)
            || tr.Player.LastName!.Contains(filter!.Search!));
    }
}
