using Api.Attributes;
using System.ComponentModel.DataAnnotations;

namespace Api.Models.V1;

/// <summary> Base team data. </summary>
/// <param name="Country">Must be a valid ISO 3166-1 alpha-2 country code (e.g., US, GB)</param>
/// <param name="Name">The display name of the team.</param>
/// <remarks>
/// Validation attributes on base positional records are not inherited by child parameters; 
/// they must be re-applied to the child record's primary constructor.
/// </remarks>
public abstract record CreateUpdateTeamModel(
    string? Country,
    string? Name);

/// <summary> Data for creating a new team. </summary>
/// <inheritdoc cref="CreateUpdateTeamModel" />
/// <param name="Id">Unique team id</param>
/// <param name="Country"><inheritdoc /></param>
/// <param name="Name"><inheritdoc /></param>
/// <param name="TransferBudget">The initial budget; defaults to 5,000,000.</param>
/// <param name="Players">An optional initial list of players for the team.</param>
public record CreateTeamModel(
    Guid Id,
    [CountryCode(ErrorMessage = Constants.CountryCodeErrorMessage)] string? Country,
    [MinLength(Domain.Constants.StringMinLength)][MaxLength(Domain.Constants.StringMaxLength)] string? Name,
    [Range(0, int.MaxValue)][Required] decimal TransferBudget = Domain.Constants.InitialTeamTransferBudget,   
    [MaxLength(Constants.MaxLengthOfPlayers)] IReadOnlyCollection<CreatePlayerModel>? Players = null)
    : CreateUpdateTeamModel(Country, Name)
{
    public IReadOnlyCollection<CreatePlayerModel> Players { get; init; } = Players ?? [];
}

/// <summary> Data for updating a team's details. </summary>
/// <inheritdoc cref="CreateUpdateTeamModel" />
/// <param name="Country"><inheritdoc /></param>
/// <param name="Name"><inheritdoc /></param>
/// <param name="ConcurrencyStamp">The stamp used for optimistic concurrency checks.</param>
public record UpdateTeamModel(
    [CountryCode(ErrorMessage = Constants.CountryCodeErrorMessage)] string? Country,
    [MinLength(Domain.Constants.StringMinLength)][MaxLength(Domain.Constants.StringMaxLength)] string? Name,
    [MaxLength(Domain.Constants.StringMaxLength)][Required] string ConcurrencyStamp)
    : CreateUpdateTeamModel(Country, Name);
