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
    string? ConcurrencyStamp) { }