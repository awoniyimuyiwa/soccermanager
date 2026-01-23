using System.ComponentModel.DataAnnotations;

namespace Api.Models.V1;

public record PayForTransferModel
{
    // Just one team per user for now, so no need for team id yet


    [MaxLength(Domain.Constants.StringMaxLength)]
    [Required]
    public string ConcurrencyStamp { get; set; } = null!;
}