using Domain;

namespace Api.Models.V1;

public record PlayerModel(
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

public record PlayersModel
{
    public IReadOnlyCollection<PlayerModel> Players { get; init; } = [];
}

