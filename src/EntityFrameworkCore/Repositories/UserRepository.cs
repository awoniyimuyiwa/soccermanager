using Domain;
using EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace EntityFrameworkCore.Repositories;

class UserRepository(ApplicationDbContext context) : IUserRepository
{
    readonly ApplicationDbContext _context = context;

    public async Task<ApplicationUser?> Find(
        Expression<Func<ApplicationUser, bool>> expression,
        bool forUpdate = false,
        string[]? includes = null,
        CancellationToken cancellationToken = default)
    {
        IQueryable<ApplicationUser> query = _context.Set<ApplicationUser>();
        if (!forUpdate) query = query.AsNoTracking();
        if (includes != null)
            foreach (var path in includes) query = query.Include(path);

        return await query.FirstOrDefaultAsync(expression, cancellationToken);
    }

    public async Task<AISettingDto?> GetAISetting(
        long userId, 
        CancellationToken cancellationToken = default)
    {
        var dto = await _context.Users
            .Where(u => u.Id == userId)
            .Select(u => u.AISetting == null ? null : new AISettingDto(
                u.AISetting.ExternalId,
                u.AISetting.CustomEndpoint,
                u.AISetting.Key,
                u.AISetting.Model,
                u.AISetting.Provider,
                u.AISetting.CreatedAt,
                u.AISetting.UpdatedAt))
            .FirstOrDefaultAsync(cancellationToken);

        return dto;
    }

    public Task<PaginatedList<UserDto>> Paginate(
        UserFilterDto? filter,
        int pageNumber = Domain.Constants.MinPageNumber,
        int pageSize = Domain.Constants.MaxPageSize,
        CancellationToken cancellationToken = default) => 
        
        _context.Set<ApplicationUser>()
        .ToPaginatedList<
            ApplicationUser, 
            UserDto, 
            UserDto>(
            pageNumber,
            pageSize,
            q => q.ToDto(),
            u => u.CreatedAt,
            filter: q => filter != null ? ApplyFilter(filter) : q,
            cancellationToken);

    public Task<CursorList<UserDto>> Stream(
        UserFilterDto? filter,
        Cursor? cursor,
        int pageSize = Domain.Constants.MaxPageSize,
        CancellationToken cancellationToken = default) => 
       
        _context.Set<ApplicationUser>()
        .ToCursorList<
            ApplicationUser, 
            UserDto, 
            UserDto>(
            cursor,
            pageSize,
            q => q.ToDto(),
            filter: q => filter != null ? ApplyFilter(filter) : q,
            cancellationToken);

    private IQueryable<ApplicationUser> ApplyFilter(UserFilterDto filter)
    {
        return _context.Set<ApplicationUser>()
            .WhereIf(!string.IsNullOrWhiteSpace(filter.SearchTerm),
               u => (u.Email != null && u.Email.Contains(filter.SearchTerm!))
                       || (u.UserName != null && u.UserName.Contains(filter.SearchTerm!))
                       || (u.FirstName != null && u.FirstName.Contains(filter.SearchTerm!))
                       || (u.LastName != null && u.LastName.Contains(filter.SearchTerm!)))
             .WhereIf(filter.IsEmailConfirmed.HasValue, user => user.EmailConfirmed == filter.IsEmailConfirmed)
             .WhereIf(filter.CreatedFrom.HasValue, user => user.CreatedAt >= filter.CreatedFrom!.Value)
             .WhereIf(filter.CreatedTo.HasValue, user => user.CreatedAt <= filter.CreatedTo!.Value)
             .WhereIf(filter.UpdatedFrom.HasValue, user => user.UpdatedAt >= filter.UpdatedFrom!.Value)
             .WhereIf(filter.UpdatedTo.HasValue, user => user.UpdatedAt <= filter.UpdatedTo!.Value);
    }
}
