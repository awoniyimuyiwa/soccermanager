using Domain;
using EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.Repositories;

class UserRepository(ApplicationDbContext context) : IUserRepository
{
    private readonly ApplicationDbContext _context = context;

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
