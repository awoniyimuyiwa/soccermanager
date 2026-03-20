namespace Domain;

/// <summary>
/// For admins
/// </summary>
public record UserDto(
    string? Email,
    string FirstName,
    long Id,
    bool IsEmailConfirmed,
    string LastName,
    DateTimeOffset? LockoutEnd,
    string? UserName,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    string? ConcurrencyStamp) : IHasInternalCursor 
{
    long IHasInternalCursor.InternalId => Id;
}

public record UserFilterDto(
    string SearchTerm = "",
    DateTimeOffset? CreatedFrom = null,
    DateTimeOffset? CreatedTo = null,
    bool? IsEmailConfirmed = null,
    DateTimeOffset? UpdatedFrom = null,
    DateTimeOffset? UpdatedTo = null);