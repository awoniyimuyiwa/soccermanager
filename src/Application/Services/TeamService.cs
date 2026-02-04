using Application.Contracts;
using Domain;
namespace Application.Services;

class TeamService(
    IPlayerRepository playerRepository,
    ITeamRepository teamRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ITeamService
{
    private readonly IPlayerRepository _playerRepository = playerRepository;
    readonly ITeamRepository _teamRepository = teamRepository;
    readonly IUnitOfWork _unitOfWork = unitOfWork;
    readonly TimeProvider _timeProvider = timeProvider;

    public async Task<IReadOnlyCollection<PlayerDto>> AddPlayers(
        Guid teamId,
        Guid userId,
        AddPlayersDto input,
        CancellationToken cancellationToken = default)
    {
        var team = await _teamRepository.Find(
            t => t.Id == teamId && t.OwnerId == userId,
            true,
            null,
            cancellationToken) ?? throw new EntityNotFoundException(nameof(Team), teamId);

        var players = AddPlayers(
            team,
            input.Players);
        team.ConcurrencyStamp = input.TeamConcurrencyStamp;

        await _unitOfWork.SaveChanges(cancellationToken);

        return [.. players.Select(p => p.ToDto(DateOnly.FromDateTime(_timeProvider.GetUtcNow().Date)))];
    }

    public async Task<TeamDto> Create(
        ApplicationUser owner,
        CreateTeamDto teamDto,
        IReadOnlyCollection<CreatePlayerDto> playerDtos,
        CancellationToken cancellationToken = default)
    {
        var team = new Team(
            Guid.NewGuid(),
            teamDto.Country,
            teamDto.Name,
            owner);
        _teamRepository.Add(team);

        team.TransferBudget += teamDto.TransferBudget;
        _teamRepository.AddTransferBudgetValue(new TransferBudgetValue(
            Guid.NewGuid(),
            team.Id,
            teamDto.TransferBudget,
            Constants.InitialValueDescription));

        AddPlayers(
            team,
            playerDtos);

        await _unitOfWork.SaveChanges(cancellationToken);

        return team.ToDto();
    }

    public async Task<TeamDto> Update(
        Guid teamId,
        Guid userId,
        UpdateTeamDto input,
        CancellationToken cancellationToken = default)
    {
        var team = await _teamRepository.Find(
            t => t.Id == teamId && t.OwnerId == userId,
            true,
            ["Owner"],
            cancellationToken) ?? throw new EntityNotFoundException(nameof(Team), teamId);

        if (!string.IsNullOrWhiteSpace(input.Country))
        {
            team.Country = input.Country;
        }

        if (!string.IsNullOrWhiteSpace(input.Name))
        {
            team.Name = input.Name;
        }

        team.ConcurrencyStamp = input.ConcurrencyStamp;

        await _unitOfWork.SaveChanges(cancellationToken);

        return team.ToDto();
    }

    private List<Player> AddPlayers( 
        Team team,
        IReadOnlyCollection<CreatePlayerDto> playerDtos)
    {
        var players = new List<Player>();

        foreach (var playerDto in playerDtos)
        {
            var player = new Player(
                Guid.NewGuid(),
                playerDto.Country,
                playerDto.DateOfBirth,
                playerDto.FirstName,
                playerDto.LastName,
                team,
                playerDto.Type);

            player.Value += playerDto.Value;
            team.Value += player.Value;
            _playerRepository.Add(player);
            _playerRepository.AddPlayerValue(new PlayerValue()
            {
                PlayerId = player.Id,
                Type = PlayerValueType.Initial,
                Value = playerDto.Value
            });

            players.Add(player);
        }

        return players;
    }
}