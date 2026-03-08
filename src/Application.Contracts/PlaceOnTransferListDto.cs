namespace Application.Contracts;

public record PlaceOnTransferListDto
{
    public virtual int AskingPrice { get; init; }

    /// <summary>
    /// Player concurrency stamp to ensure the player is not modified by another process between the time it was retrieved and the time the transfer is attempted.
    /// </summary>
    public virtual string PlayerConcurrencyStamp { get; init; } = null!;
}
