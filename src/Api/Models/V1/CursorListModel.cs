namespace Api.Models.V1;

public record CursorListModel<T>(
    IReadOnlyCollection<T> Items,
    string? NextCursor,
    int PageSize) { }