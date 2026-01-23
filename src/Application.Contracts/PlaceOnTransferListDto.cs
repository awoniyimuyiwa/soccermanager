namespace Application.Contracts;

public record PlaceOnTransferListDto
{
    public virtual int AskingPrice { get; set; }

    /// <summary>
    /// Player concurrency stamp
    /// </summary>
    public virtual string PlayerConcurrencyStamp { get; set; } = null!;
}
