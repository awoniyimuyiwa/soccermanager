namespace Application.Contracts;

public record AddPlayersDto
{
    public IReadOnlyCollection<CreatePlayerDto> Players { get; set; } = [];

    public string TeamConcurrencyStamp { get; set; } = null!;
}
