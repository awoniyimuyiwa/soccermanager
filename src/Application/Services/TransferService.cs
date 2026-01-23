using Application.Contracts;
using Domain;
namespace Application.Services;

class TransferService(
    IPlayerRepository playerRepository,
    ITeamRepository teamRepository,
    ITransferRepository transferRepository,
    IUnitOfWork unitOfWork) : ITransferService
{
    readonly IPlayerRepository _playerRepository = playerRepository;
    readonly ITeamRepository _teamRepository = teamRepository;
    readonly ITransferRepository _transferRepository = transferRepository;
    readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<TransferDto> Pay(
        Guid id, 
        Guid userId,
        string concurrencyStamp,
        CancellationToken cancellationToken = default)
    {
        var transfer = await _transferRepository.Find(
            t => t.Id == id,
            true,
            null,
            cancellationToken) ?? throw new EntityNotFoundException(nameof(Transfer), id);
        
        if (transfer.ToTeamId is not null)
        {
            throw new DomainException(Constants.TransferAlreadyCompletedErrorMessage);
        }

        // Only one team per user for now
        var toTeam = await _teamRepository.Find(
            t => t.OwnerId == userId,
            true,
            null,
            cancellationToken) ?? throw new DomainException(Constants.MustOwnATeamForTransferErrorMessage);
        
        if (toTeam.Id == transfer.FromTeamId)
        {
            throw new DomainException(Constants.TransferCantBeToTheSameTeamErrorMessage);
        }

        if (toTeam.TransferBudget - transfer.AskingPrice < 0)
        {
            throw new DomainException(Constants.TransferBudgetIsInsufficientErrorMessage);
        }
        // Add transfer budget value for audit, update transfer budget directly without loading value collections to improve performance.
        toTeam.TransferBudget -= transfer.AskingPrice; 
        _teamRepository.AddTransferBudgetValue(new TransferBudgetValue(
            Guid.NewGuid(),
            toTeam.Id,
            -transfer.AskingPrice,
            Constants.TransferDescription,
            transfer));
        
        var player = await _playerRepository.Find(
            p => p.Id == transfer.PlayerId,
            true,
            null,
            cancellationToken) ?? throw new EntityNotFoundException(nameof(Player), transfer.PlayerId);

        var fromTeam = await _teamRepository.Find(
            t => t.Id == transfer.FromTeamId,
            true,
            null,
            cancellationToken) ?? throw new EntityNotFoundException(nameof(Team), transfer.FromTeamId);
        // Update team value directly without loading value collections to improve performance.     
        fromTeam.Value -= player.Value;

        var playerValueIncreament = (new Random().Next(Constants.MinPlayerValuePercentageIncrease, Constants.MaxPlayerValuePercentageIncrease + 1) / 100m) * player.Value;
        player.Value += playerValueIncreament;
        toTeam.Value += player.Value;
        _playerRepository.AddPlayerValue(new PlayerValue()
        {
            PlayerId = player.Id,
            Value = playerValueIncreament,
            Type = PlayerValueType.Transfer,
            SourceEntityId = transfer.Id
        });

        // Update references only, adding to collections manually when changing relationships may cause EF Core change tracking issues.
        player.Team = toTeam;
        transfer.ToTeam = toTeam;

        transfer.ConcurrencyStamp = concurrencyStamp;

        await _unitOfWork.SaveChanges(cancellationToken);

        return transfer.ToDto();
    }
}
