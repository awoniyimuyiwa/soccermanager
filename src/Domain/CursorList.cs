namespace Domain;

public record CursorList<T>(
    IReadOnlyCollection<T> Items,
    [property: Protected] string? Next, // Will be null at the end of the data
    int PageSize) {}

