using Domain;

namespace Api.Models.V1;

public record CursorListModel<T>(
    IReadOnlyCollection<T> Items,
    [property: Protected] string? Next,
    int PageSize) where T : class;

