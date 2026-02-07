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
        PayForTransferDto input,
        CancellationToken cancellationToken = default)
    {
        var transfer = await _transferRepository.Find(
            t => t.ExternalId == id,
            true,
            ["FromTeam", "Player"],
            cancellationToken) ?? throw new EntityNotFoundException(nameof(Transfer), id);
        
        if (transfer.ToTeamId is not null)
        {
            throw new DomainException(Constants.TransferAlreadyCompletedErrorMessage);
        }

        if (input.ToTeamId == transfer.FromTeam.ExternalId)
        {
            throw new DomainException(Constants.TransferCantBeToTheSameTeamErrorMessage);
        }

        var toTeam = await _teamRepository.Find(
            t => t.ExternalId == input.ToTeamId,
            true,
            null,
            cancellationToken) ?? throw new EntityNotFoundException(nameof(Team), input.ToTeamId);
        
        if (toTeam.TransferBudget - transfer.AskingPrice < 0)
        {
          throw new DomainException(Constants.InsufficientTeamTransferBudgetErrorMessage);
        }

        // Decrease transfer budget by asking price for destination team
        _teamRepository.AddTransferBudgetValue(new TransferBudgetValue(
            Guid.NewGuid(),
            toTeam,
            -transfer.AskingPrice,
            Constants.TransferDescription,
            transfer));
            
        var playerValueIncreament = (new Random().Next(Constants.MinPlayerValuePercentageIncrease, Constants.MaxPlayerValuePercentageIncrease + 1) / 100m) * transfer.Player.Value;
        _playerRepository.AddPlayerValue(new PlayerValue(
            Guid.NewGuid(),
            transfer.Player, 
            PlayerValueType.Transfer, 
            playerValueIncreament, 
            transfer.Id));

        // Update references only, adding to collections manually when changing relationships may cause EF Core change tracking issues.
        transfer.Player.Team = toTeam;
        transfer.ToTeam = toTeam;

        transfer.ConcurrencyStamp = input.ConcurrencyStamp;

        await _unitOfWork.SaveChanges(cancellationToken);

        return transfer.ToDto();
    }
}
