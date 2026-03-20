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
        UserFilterDto? filter,
        int pageNumber = Constants.MinPageNumber,
        int pageSize = Constants.MaxPageSize,
        CancellationToken cancellationToken = default);

    Task<CursorList<UserDto>> Stream(
        UserFilterDto? filter, 
        Cursor? cursor, 
        int pageSize, 
        CancellationToken cancellationToken = default);
}