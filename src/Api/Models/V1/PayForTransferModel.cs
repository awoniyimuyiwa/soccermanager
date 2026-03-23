using System.ComponentModel.DataAnnotations;

namespace Api.Models.V1;

/// <summary>
/// Data for processing a transfer payment.
/// </summary>
/// <param name="ToTeamId">The unique identifier of the team receiving the player.</param>
/// <param name="ConcurrencyStamp">Stamp to ensure the transfer wasn't modified since retrieval.</param>
public record PayForTransferModel(
    [Required]
    Guid ToTeamId,

    [MaxLength(Domain.Constants.StringMaxLength)]
    [Required]
    string ConcurrencyStamp);