using Api.ValidationAttributes;
using Application.Contracts;
using System.ComponentModel.DataAnnotations;

namespace Api.Models.V1;

public record CreatePlayersModel
{
    [MaxLength(Constants.MaxLengthOfPlayers)]
    [Required]
    public IReadOnlyCollection<CreatePlayerModel> Players { get; set; } = [];

    [MaxLength(Domain.Constants.StringMaxLength)]
    [Required]
    public string TeamConcurrencyStamp { get; set; } = null!;
}

public record CreatePlayerModel : CreatePlayerDto
{
    /// <summary>
    /// Must be a valid ISO 3166-1 alpha-2 country code (e.g., US, GB)
    /// </summary>
    [CountryCode(ErrorMessage = Constants.CountryCodeErrorMessage)]
    public override string? Country { get; set; }

    /// <summary>
    /// Must be 18 to 40
    /// </summary>
    [AgeRange(Domain.Constants.MinPlayerAge, Domain.Constants.MaxPlayerAge)]
    [Required]
    public override DateOnly DateOfBirth { get; set; }

    [MinLength(Domain.Constants.StringMinLength)]
    [MaxLength(Domain.Constants.StringMaxLength)]
    public override string? FirstName { get; set; }

    [MinLength(Domain.Constants.StringMinLength)]
    [MaxLength(Domain.Constants.StringMaxLength)]
    public override string? LastName { get; set; }

    /// <summary>
    /// Default is 1,000,000
    /// </summary>
    [Range(1, int.MaxValue)]
    [Required]
    public override decimal Value { get; set; } = Domain.Constants.InitialPlayerValue;
}


