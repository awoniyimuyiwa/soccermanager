namespace Domain;

public interface IUserRepository
{
    Task<PaginatedList<UserDto>> Paginate(
        string searchTerm = "",
        int pageNumber = Constants.MinPageNumber,
        int pageSize = Constants.MaxPageSize,
        CancellationToken cancellationToken = default);
}