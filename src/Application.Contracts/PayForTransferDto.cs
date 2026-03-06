namespace Application.Contracts;

public record PayForTransferDto
{
    public virtual Guid ToTeamId { get; init; }

    /// <summary>
    /// Transfer concurrency stamp to ensure the transfer is not modified by another process between the time it was retrieved and the time the payment is attempted.
    /// </summary>
    public virtual string ConcurrencyStamp { get; init; } = null!;
}
