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

    public async Task<PaginatedList<FullTransferDto>> Paginate(
        TransferFilterDto filter,
        int pageNumber = 1,
        int pageSize = 100,
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
            ? ApplyFilter(filter) : _context.Set<Transfer>();

        var count = await query
            .Take(Domain.Constants.MaxRowsToSkip + 1)
            .CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToInternalDto()
            .ToListAsync(cancellationToken);

        return new PaginatedList<FullTransferDto>(
            items,
            count,
            pageNumber,
            pageSize);
    }

    public async Task<CursorList<FullTransferDto>> Stream(
        TransferFilterDto? filter,
        Cursor? cursor,
        int pageSize = Domain.Constants.MaxPageSize,
        CancellationToken cancellationToken = default)
    {
        pageSize = Math.Clamp(
            pageSize,
            Domain.Constants.MinPageSize,
            Domain.Constants.MaxPageSize);

        var query = filter is not null
            ? ApplyFilter(filter) : _context.Set<Transfer>();

        // Descending order, newest first
        var items = await query
           .OrderByDescending(tr => tr.CreatedAt)
           .ThenByDescending(tr => tr.Id)
           .WhereIf(cursor != null, tr => tr.CreatedAt < cursor!.LastCreatedAt || (tr.CreatedAt == cursor.LastCreatedAt && tr.Id < cursor!.LastId))
           .Take(pageSize)
           .ToInternalDto()
           .ToListAsync(cancellationToken);

        var last = items.LastOrDefault();
        var next = last != null
            ? new Cursor(last.InternalId, last.CreatedAt)
            : null;

        return new CursorList<FullTransferDto>(
            items,
            next?.ToJson(),
            pageSize);
    }

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
