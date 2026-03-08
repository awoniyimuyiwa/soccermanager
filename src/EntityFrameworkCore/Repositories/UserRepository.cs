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

    public async Task<PaginatedList<UserDto>> Paginate(
        string searchTerm = "",
        int pageNumber = Domain.Constants.MinPageNumber,
        int pageSize = Domain.Constants.MaxPageSize,
        CancellationToken cancellationToken = default)
    {
        pageNumber = Math.Max(Domain.Constants.MinPageNumber, pageNumber);

        pageSize = Math.Clamp(
            pageSize, 
            Domain.Constants.MinPageSize, 
            Domain.Constants.MaxPageSize);
        
        var query = _context.Set<ApplicationUser>()
            .AsNoTracking()
            .WhereIf(!string.IsNullOrWhiteSpace(searchTerm),
               user => (user.Email != null && user.Email.Contains(searchTerm!))
                       || (user.UserName != null && user.UserName.Contains(searchTerm!))
                       || (user.FirstName != null && user.FirstName.Contains(searchTerm!))
                       || (user.LastName != null && user.LastName.Contains(searchTerm!)));
       
        var count = await query.CountAsync(cancellationToken);
        
        var items = await query
            .OrderByDescending(u => u.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new UserDto(
                u.Email,
                //u.ExternalId,
                u.FirstName,
                u.Id,
                u.EmailConfirmed,
                u.LastName,
                u.LockoutEnd,
                u.UserName,
                u.ConcurrencyStamp))
            .ToListAsync(cancellationToken);
     
        return new PaginatedList<UserDto>(
            items,
            count,
            pageNumber,
            pageSize);
    }
}
