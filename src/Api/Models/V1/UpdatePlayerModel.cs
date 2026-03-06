using Api.Attributes;
using Application.Contracts;
using Domain;
using System.ComponentModel.DataAnnotations;

namespace Api.Models.V1;

public record UpdatePlayerModel : UpdatePlayerDto
{
    /// <summary>
    /// Must be a valid ISO 3166-1 alpha-2 country code (e.g., US, GB)
    /// </summary>
    [CountryCode(ErrorMessage = Constants.CountryCodeErrorMessage)]
    public override string? Country { get; init; }

    /// <summary>
    /// Must be 18 to 40
    /// </summary>
    [AgeRange(Domain.Constants.MinPlayerAge, Domain.Constants.MaxPlayerAge)]
    [Required]
    public override DateOnly DateOfBirth { get; init; }

    [MinLength(Domain.Constants.StringMinLength)]
    [MaxLength(Domain.Constants.StringMaxLength)]
    public override string? FirstName { get; init; }

    [MinLength(Domain.Constants.StringMinLength)]
    [MaxLength(Domain.Constants.StringMaxLength)]
    public override string? LastName { get; init; }

    [Required]
    [EnumDataType(typeof(PlayerType))]
    public override int Type { get; init; }

    [MaxLength(Domain.Constants.StringMaxLength)]
    [Required]
    public override string ConcurrencyStamp { get; init; } = null!;
}