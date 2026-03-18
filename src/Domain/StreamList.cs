namespace Domain;

public record StreamList<T>(
    IReadOnlyCollection<T> Items,
    string? NextCursor); // Null when there's no more data