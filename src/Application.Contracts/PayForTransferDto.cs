namespace Application.Contracts;

/// <summary>
/// DTO for processing a transfer payment.
/// </summary>
/// <param name="ToTeamId">The unique identifier of the team receiving the player.</param>
/// <param name="ConcurrencyStamp">Stamp to ensure the transfer hasn't been modified since retrieval.</param>
public record PayForTransferDto(
    Guid ToTeamId,
    string ConcurrencyStamp);

