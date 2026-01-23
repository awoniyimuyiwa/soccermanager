using Application.Contracts;
using Domain;
namespace Application.Services;

class TeamService(
    ITeamRepository teamRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ITeamService
{
    readonly ITeamRepository _teamRepository = teamRepository;
    readonly IUnitOfWork _unitOfWork = unitOfWork;
    readonly TimeProvider _timeProvider = timeProvider;

    public Task<TeamDto> CreateDefault(ApplicationUser owner)
    {
        var team = new Team(
            Guid.NewGuid(),
            null,
            null,
            owner,
            _timeProvider.GetUtcNow().Date);

        _teamRepository.Add(team);

        return Task.FromResult(team.ToDto());
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
}