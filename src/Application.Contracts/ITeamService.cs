using Domain;

namespace Application.Contracts;

public interface ITeamService
{
    Task<TeamDto> CreateDefault(ApplicationUser owner);

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
