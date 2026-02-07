using Application.Contracts;
using System.ComponentModel.DataAnnotations;

namespace Api.Models.V1;

public record PayForTransferModel : PayForTransferDto
{
    [MaxLength(Domain.Constants.StringMaxLength)]
    [Required]
    public override string ConcurrencyStamp { get; set; } = null!;

    [Required]
    public override Guid ToTeamId { get; set; }
}