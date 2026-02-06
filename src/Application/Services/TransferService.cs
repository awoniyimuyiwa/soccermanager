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
        long userId,
        string concurrencyStamp,
        CancellationToken cancellationToken = default)
    {
        var transfer = await _transferRepository.Find(
            t => t.ExternalId == id,
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

        // Add transfer budget value for audit
        _teamRepository.AddTransferBudgetValue(new TransferBudgetValue(
            Guid.NewGuid(),
            toTeam,
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
      
        var playerValueIncreament = (new Random().Next(Constants.MinPlayerValuePercentageIncrease, Constants.MaxPlayerValuePercentageIncrease + 1) / 100m) * player.Value;
        _playerRepository.AddPlayerValue(new PlayerValue(
            Guid.NewGuid(),
            player, 
            PlayerValueType.Transfer, 
            playerValueIncreament, 
            transfer.Id));

        // Update references only, adding to collections manually when changing relationships may cause EF Core change tracking issues.
        player.Team = toTeam;
        transfer.ToTeam = toTeam;

        transfer.ConcurrencyStamp = concurrencyStamp;

        await _unitOfWork.SaveChanges(cancellationToken);

        return transfer.ToDto();
    }
}
