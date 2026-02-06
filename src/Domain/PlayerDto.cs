namespace Domain;

public record PlayerDto(
    Guid Id,
    int Age,
    string? Country,
    DateOnly DateOfBirth,
    string? FirstName,
    string? LastName,
    Guid TeamId,
    string? TeamName,
    PlayerType Type,
    decimal Value,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    string? ConcurrencyStamp) { }
