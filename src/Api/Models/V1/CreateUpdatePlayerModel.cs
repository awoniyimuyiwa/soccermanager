using Api.Attributes;
using Domain;
using System.ComponentModel.DataAnnotations;

namespace Api.Models.V1;

/// <summary>
/// Data required to create a new player.
/// </summary>
/// <param name="Country">Must be a valid ISO 3166-1 alpha-2 country code (e.g., US, GB)</param>
/// <param name="DateOfBirth">Must be between 18 and 40 years old.</param>
/// <param name="FirstName">The player's first name.</param>
/// <param name="LastName">The player's last name.</param>
/// <param name="Type">
/// The position or role of the player:
/// 0 = <see cref="PlayerType.Goalkeeper"/>, 
/// 1 = <see cref="PlayerType.Defender"/>, 
/// 2 = <see cref="PlayerType.Midfielder"/>, 
/// 3 = <see cref="PlayerType.Attacker"/>.
/// </param>
public record CreateUpdatePlayerModel(
    [CountryCode(ErrorMessage = Constants.CountryCodeErrorMessage)] [MaxLength(2)] string? Country,

    [AgeRange(Domain.Constants.MinPlayerAge, Domain.Constants.MaxPlayerAge)] [Required] DateOnly DateOfBirth,

    [MinLength(Domain.Constants.StringMinLength)] [MaxLength(Domain.Constants.StringMaxLength)] string? FirstName,

    [MinLength(Domain.Constants.StringMinLength)] [MaxLength(Domain.Constants.StringMaxLength)] string? LastName,

    [Required] [EnumDataType(typeof(PlayerType))] int Type);


/// <summary>
/// Data required to create a new player.
/// <inheritdoc cref="CreateUpdatePlayerModel" />
/// <param name="Country"><inheritdoc /></param>
/// <param name="DateOfBirth"><inheritdoc /></param>
/// <param name="FirstName"><inheritdoc /></param>
/// <param name="LastName"><inheritdoc /></param>
/// <param name="Type"><inheritdoc /></param>
/// <param name="Value">The market value; defaults to 1,000,000.</param>
public record CreatePlayerModel(
    string? Country,
    DateOnly DateOfBirth,
    string? FirstName,
    string? LastName,
    int Type,
    [Range(1, int.MaxValue)][Required] decimal Value = Domain.Constants.InitialPlayerValue)
    : CreateUpdatePlayerModel(
        Country,
        DateOfBirth,
        FirstName,
        LastName,
        Type);

public record CreatePlayersModel(
    [MaxLength(Domain.Constants.StringMaxLength)] [Required] string TeamConcurrencyStamp,
    IReadOnlyCollection<CreatePlayerModel>? Players = null)
{
    [Required]
    [MaxLength(Constants.MaxLengthOfPlayers)]
    public IReadOnlyCollection<CreatePlayerModel> Players { get; init; } = Players ?? [];
}

/// <summary> Data for updating an existing player. </summary>
/// <inheritdoc cref="CreateUpdatePlayerModel" />
/// <param name="Country"><inheritdoc /></param>
/// <param name="DateOfBirth"><inheritdoc /></param>
/// <param name="FirstName"><inheritdoc /></param>
/// <param name="LastName"><inheritdoc /></param>
/// <param name="Type"><inheritdoc /></param>
/// <param name="ConcurrencyStamp">The stamp used for optimistic concurrency checks.</param>
public record UpdatePlayerModel(
    string? Country,
    DateOnly DateOfBirth,
    string? FirstName,
    string? LastName,
    int Type,
    [MaxLength(Domain.Constants.StringMaxLength)][Required] string ConcurrencyStamp)
    : CreateUpdatePlayerModel(
        Country,
        DateOfBirth,
        FirstName,
        LastName,
        Type);
