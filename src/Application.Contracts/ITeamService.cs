using Domain;

namespace Application.Contracts;

public interface ITeamService
{
    /// <summary>
    /// Add players to team
    /// </summary>
    /// <param name="teamId"></param>
    /// <param name="userId"></param>
    /// <param name="input"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>Player details</returns>
    Task<IReadOnlyCollection<PlayerDto>> AddPlayers(
        Guid teamId,
        Guid userId,
        AddPlayersDto input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Create team
    /// </summary>
    /// <param name="teamDto"></param>
    /// <param name="playerDtos"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>Team details</returns>
    Task<TeamDto> Create(
       ApplicationUser owner,
       CreateTeamDto teamDto,
       IReadOnlyCollection<CreatePlayerDto> playerDtos,
       CancellationToken cancellationToken = default);

    /// <summary>
    /// Update team
    /// </summary>
    /// <param name="teamId"></param>
    /// <param name="userId"></param>
    /// <param name="input"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>Team details</returns>
    /// <exception cref="ConcurrencyException">When concurrency stamp specified does not match the one currently in storage</exception>
    /// <exception cref="EntityNotFoundException">When an entity is not found</exception>
    Task<TeamDto> Update(
        Guid teamId,
        Guid userId,        
        UpdateTeamDto input,
        CancellationToken cancellationToken = default);
}
