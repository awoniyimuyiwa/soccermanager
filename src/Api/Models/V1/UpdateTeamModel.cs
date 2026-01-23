using Api.ValidationAttributes;
using Application.Contracts;
using System.ComponentModel.DataAnnotations;

namespace Api.Models.V1;

public record UpdateTeamModel : UpdateTeamDto
{
    /// <summary>
    /// Must be a valid ISO 3166-1 alpha-2 country code (e.g., US, GB)
    /// </summary>
    [CountryCode(ErrorMessage = Constants.CountryCodeErrorMessage)]
    public override string? Country { get; set; }

    [MinLength(Domain.Constants.StringMinLength)]
    [MaxLength(Domain.Constants.StringMaxLength)]
    public override string? Name { get; set; }

    [MaxLength(Domain.Constants.StringMaxLength)]
    [Required]
    public override string ConcurrencyStamp { get; set; } = null!;
}
