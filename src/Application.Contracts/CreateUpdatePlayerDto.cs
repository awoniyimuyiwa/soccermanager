using Domain;

namespace Application.Contracts;

public record CreateUpdatePlayerDto
{
    public virtual string? Country { get; set; }

    public virtual DateOnly DateOfBirth { get; set; }

    public virtual string? FirstName { get; set; }

    public virtual string? LastName { get; set; }

    public virtual PlayerType Type { get; set; }
}

