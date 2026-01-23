namespace Domain;

public class PaginatedList<T>(
    IReadOnlyCollection<T> items,
    int count,
    int pageNumber, 
    int pageSize) where T : class
{
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
    public int PageNumber { get; } = pageNumber;

    public int TotalCount { get; } = count;

    public int TotalPages { get; } = (int)Math.Ceiling(count / (double)pageSize);
    
    public IReadOnlyCollection<T> Items { get; } = items;
}