using Application.Contracts;
using Domain;
namespace Application.Services;

class TeamService(
    IPlayerRepository playerRepository,
    ITeamRepository teamRepository,
    IUnitOfWork unitOfWork) : ITeamService
{
    private readonly IPlayerRepository _playerRepository = playerRepository;
    readonly ITeamRepository _teamRepository = teamRepository;
    readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<IReadOnlyCollection<PlayerDto>> AddPlayers(
        Guid teamId,
        long userId,
        AddPlayersDto input,
        CancellationToken cancellationToken = default)
    {
        var team = await _teamRepository.Find(
            t => t.ExternalId == teamId && t.OwnerId == userId,
            true,
            ["Owner"],
            cancellationToken) ?? throw new EntityNotFoundException(nameof(Team), teamId);

        var players = AddPlayers(
            team,
            input.Players);
        team.ConcurrencyStamp = input.TeamConcurrencyStamp;

        await _unitOfWork.SaveChanges(cancellationToken);

        // Fetch the final trigger-computed values from the DB
        var playerIds = players.Select(p => p.Id).ToList();
        return await _playerRepository.GetAll(
            p => playerIds.Contains(p.Id),
            cancellationToken);
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

        _teamRepository.AddTransferBudgetValue(new TransferBudgetValue(
            Guid.NewGuid(),
            team,
            teamDto.TransferBudget,
            Constants.InitialValueDescription));

        AddPlayers(
            team,
            playerDtos);

        await _unitOfWork.SaveChanges(cancellationToken);

        // Fetch the final trigger-computed values from the DB
        // another way is: return team.ToDto() with { TransferBudget = teamDto.TransferBudget, Value = playerDtos.Sum(p => p.Value) }
        await _teamRepository.Reload(team, cancellationToken);

        return team.ToDto();
    }

    public async Task<TeamDto> Update(
        Guid teamId,
        long userId,
        UpdateTeamDto input,
        CancellationToken cancellationToken = default)
    {
        var team = await _teamRepository.Find(
            t => t.ExternalId == teamId && t.OwnerId == userId,
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

        // Fetch the final trigger-computed values from the DB
        await _teamRepository.Reload(team, cancellationToken);

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

            _playerRepository.Add(player);
            _playerRepository.AddPlayerValue(new PlayerValue(
                Guid.NewGuid(), 
                player,
                PlayerValueType.Initial,
                playerDto.Value));

            players.Add(player);
        }

        return players;
    }
}