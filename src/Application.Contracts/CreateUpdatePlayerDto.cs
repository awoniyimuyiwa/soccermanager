
namespace Application.Contracts;

public record CreateUpdatePlayerDto
{
    public virtual string? Country { get; init; }

    public virtual DateOnly DateOfBirth { get; init; }

    public virtual string? FirstName { get; init; }

    public virtual string? LastName { get; init; }

    public virtual int Type { get; init; }
}

