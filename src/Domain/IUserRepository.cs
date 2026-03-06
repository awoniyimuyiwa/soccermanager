using System.Linq.Expressions;

namespace Domain;

public interface IUserRepository
{
    Task<ApplicationUser?> Find(
        Expression<Func<ApplicationUser, bool>> expression, 
        bool forUpdate = false, 
        string[]? includes = null, 
        CancellationToken cancellationToken = default);

    Task<AISettingDto?> GetAISetting(
        long userId,
        CancellationToken cancellationToken = default);

    Task<PaginatedList<UserDto>> Paginate(
        string searchTerm = "",
        int pageNumber = Constants.MinPageNumber,
        int pageSize = Constants.MaxPageSize,
        CancellationToken cancellationToken = default);
}