namespace Application.Contracts;

/// <summary>
/// DTO for placing a player on the transfer list.
/// </summary>
/// <param name="AskingPrice">The price requested for the player.</param>
/// <param name="PlayerConcurrencyStamp">Stamp to ensure the player wasn't modified since retrieval.</param>
public record PlaceOnTransferListDto(
    int AskingPrice,
    string PlayerConcurrencyStamp);
