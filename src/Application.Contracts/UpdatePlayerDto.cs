namespace Application.Contracts;

public record UpdatePlayerDto : CreateUpdatePlayerDto
{
    public virtual string ConcurrencyStamp { get; set; } = null!;
}


