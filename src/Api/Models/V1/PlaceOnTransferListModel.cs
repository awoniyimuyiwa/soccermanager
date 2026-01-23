using Application.Contracts;
using System.ComponentModel.DataAnnotations;

namespace Api.Models.V1;

public record PlaceOnTransferListModel : PlaceOnTransferListDto
{
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Asking price must be greater than zero.")]
    public override int AskingPrice { get; set; }

    /// <summary>
    /// Player concurrency stamp
    /// </summary>
    [MaxLength(Domain.Constants.StringMaxLength)]
    [Required]
    public override string PlayerConcurrencyStamp { get; set; } = null!;
}
