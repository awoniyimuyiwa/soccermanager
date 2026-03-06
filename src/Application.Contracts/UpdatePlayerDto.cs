namespace Application.Contracts;

public record UpdatePlayerDto : CreateUpdatePlayerDto
{
    public virtual string ConcurrencyStamp { get; init; } = null!;
}


