using Domain;

namespace Api.Models.V1;

public record PlayersModel
{
    public IReadOnlyCollection<PlayerDto> Players { get; set; } = [];
}


