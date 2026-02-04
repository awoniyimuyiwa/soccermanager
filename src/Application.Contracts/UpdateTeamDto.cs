namespace Application.Contracts;

public record UpdateTeamDto : CreateUpdateTeamDto
{
    public virtual string ConcurrencyStamp { get; set; } = null!;
}
