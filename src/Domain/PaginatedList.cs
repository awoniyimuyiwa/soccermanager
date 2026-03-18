namespace Domain;

public record PaginatedList<T>(
    IReadOnlyCollection<T> Items,
    int TotalCount,
    int PageNumber, 
    int PageSize) where T : class
{
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
    public int TotalPages { get; } = (int)Math.Ceiling(TotalCount / (double)PageSize);
}