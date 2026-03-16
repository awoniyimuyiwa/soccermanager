namespace Domain;

public record CursorList<T>(
    IReadOnlyCollection<T> Items,
    PageCursor? Next, // Will be null at the end of the data
    int PageSize) {}

public record PageCursor(
    long LastId,
    DateTimeOffset LastCreatedAt) {} 
