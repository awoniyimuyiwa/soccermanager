namespace Application.Contracts;

public record UpdatePlayerDto
{
    public virtual string? Country { get; set; }

    public virtual string? FirstName { get; set; }

    public virtual string? LastName { get; set; }

    public virtual string ConcurrencyStamp { get; set; } = null!;
}
