namespace Application.Contracts;

/// <summary>
/// DTO for creating or updating a team.
/// </summary>
/// <param name="Country">Must be a valid ISO 3166-1 alpha-2 country code (e.g., US, GB)</param>
/// <param name="Name">The display name of the team.</param>
public record CreateUpdateTeamDto(
    string? Country,
    string? Name);

public record CreateTeamDto(
    string? Country,
    string? Name,
    decimal TransferBudget = Domain.Constants.InitialTeamTransferBudget)
    : CreateUpdateTeamDto(Country, Name);

public record UpdateTeamDto(
    string? Country,
    string? Name,
    string ConcurrencyStamp)
    : CreateUpdateTeamDto(Country, Name);
