namespace Application.Contracts;

public record CreateUpdateTeamDto
{
    /// <summary>
    /// Must be a valid ISO 3166-1 alpha-2 country code (e.g., US, GB)
    /// </summary>
    public virtual string? Country { get; set; }

    public virtual string? Name { get; set; }
}
