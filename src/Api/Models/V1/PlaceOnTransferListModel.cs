using System.ComponentModel.DataAnnotations;

namespace Api.Models.V1;

/// <summary>
/// Data for placing a player on the transfer list.
/// </summary>
/// <param name="AskingPrice">The price requested for the player. Must be greater than zero.</param>
/// <param name="PlayerConcurrencyStamp">Stamp to ensure the player wasn't modified since retrieval.</param>
public record PlaceOnTransferListModel(
    [Range(1, int.MaxValue, ErrorMessage = "Asking price must be greater than zero.")]
    [Required]
    int AskingPrice,

    [MaxLength(Domain.Constants.StringMaxLength)]
    [Required]
    string PlayerConcurrencyStamp);
