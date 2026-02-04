using Api.ValidationAttributes;
using Application.Contracts;
using System.ComponentModel.DataAnnotations;

namespace Api.Models.V1;
public record CreateTeamModel : CreateTeamDto
{
    /// <summary>
    /// Must be a valid ISO 3166-1 alpha-2 country code (e.g., US, GB)
    /// </summary>
    [CountryCode(ErrorMessage = Constants.CountryCodeErrorMessage)]
    public override string? Country { get; set; }

    [MinLength(Domain.Constants.StringMinLength)]
    [MaxLength(Domain.Constants.StringMaxLength)]
    public override string? Name { get; set; }

    /// <summary>
    /// Default is 5,000,000
    /// </summary>

    [Range(1, int.MaxValue)]
    [Required]
    public override decimal TransferBudget { get; set; } = Domain.Constants.InitialTeamTransferBudget;

    [MaxLength(Constants.MaxLengthOfPlayers)]
    public IReadOnlyCollection<CreatePlayerModel> Players { get; set; } = [];
}

