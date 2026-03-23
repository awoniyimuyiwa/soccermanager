namespace Api.Models.V1;

public record UserModel(
    string? Email,
    string FirstName,
    long Id,
    bool IsEmailConfirmed,
    string LastName,
    DateTimeOffset? LockoutEnd,
    string? UserName,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    string? ConcurrencyStamp);




