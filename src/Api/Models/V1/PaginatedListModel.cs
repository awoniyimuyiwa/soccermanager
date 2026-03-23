namespace Api.Models.V1;

public record PaginatedListModel<T>(
    IReadOnlyCollection<T> Items,
    int TotalCount,
    int PageNumber,
    int TotalPages,
    bool HasPreviousPage,
    bool HasNextPage) where T : class;
