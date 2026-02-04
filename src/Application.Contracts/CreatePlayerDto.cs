using Domain;

namespace Application.Contracts;

public record CreatePlayerDto : CreateUpdatePlayerDto
{
    public virtual decimal Value { get; set; } = Constants.InitialPlayerValue;
}


