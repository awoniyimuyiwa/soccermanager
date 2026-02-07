namespace Application.Contracts;

public record PayForTransferDto
{
    public virtual Guid ToTeamId { get; set; }

    /// <summary>
    /// Transfer concurrency stamp to ensure the transfer is not modified by another process between the time it was retrieved and the time the payment is attempted.
    /// </summary>
    public virtual string ConcurrencyStamp { get; set; } = null!;
}
