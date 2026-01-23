namespace Application.Contracts;

public record UpdateTeamDto
{
    public virtual string? Country { get; set; }

    public virtual string? Name { get; set; }

    public virtual string ConcurrencyStamp { get; set; } = null!;
}
