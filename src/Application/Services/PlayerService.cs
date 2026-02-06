using Application.Contracts;
using Domain;

namespace Application.Services;

class PlayerService(
    IPlayerRepository playerRepository,
    ITransferRepository transferRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IPlayerService
{
    readonly IPlayerRepository _playerRepository = playerRepository;
    readonly ITransferRepository _transferRepository = transferRepository;
    readonly IUnitOfWork _unitOfWork = unitOfWork;
    readonly TimeProvider _timeProvider = timeProvider;

    public async Task<TransferDto> PlaceOnTransferList(
        Guid playerId,
        long userId,
        PlaceOnTransferListDto input,
        CancellationToken cancellationToken = default)
    {
        var player = await _playerRepository.Find(
            p => p.ExternalId == playerId
                 && p.Team.OwnerId == userId,
            true,
            ["Team"],
            cancellationToken) ?? throw new EntityNotFoundException(nameof(Player), playerId);

        var transfer = await _transferRepository.Find(
            tf => tf.Player.ExternalId == playerId
                  && tf.ToTeamId == null,
            false,
            null,
            cancellationToken);
        if (transfer is not null)
        {
            throw new DomainException(Constants.PlayerAlreadyOnTransferListErrorMessage);
        }

        transfer = new Transfer(
            Guid.NewGuid(),
            input.AskingPrice, 
            player.Team,
            player);
        
        player.ConcurrencyStamp = input.PlayerConcurrencyStamp;

        _transferRepository.Add(transfer);
        _playerRepository.Update(player);
        await _unitOfWork.SaveChanges(cancellationToken);

        return transfer.ToDto();
    }

    public async Task<PlayerDto> Update(
        Guid playerId,
        long userId,
        UpdatePlayerDto input,
        CancellationToken cancellationToken = default)
    {
        var player = await _playerRepository.Find(
            p => p.ExternalId == playerId
                 && p.Team.OwnerId == userId,
            true,
            ["Team"],
            cancellationToken) ?? throw new EntityNotFoundException(nameof(Player), playerId);

        player.DateOfBirth = input.DateOfBirth;
        player.Type = input.Type;

        if (!string.IsNullOrWhiteSpace(input.Country))
        {
            player.Country = input.Country;
        }

        if (!string.IsNullOrWhiteSpace(input.FirstName))
        {
            player.FirstName = input.FirstName;
        }

        if (!string.IsNullOrWhiteSpace(input.LastName))
        {
            player.LastName = input.LastName;
        }
      
        player.ConcurrencyStamp = input.ConcurrencyStamp;

        await _unitOfWork.SaveChanges(cancellationToken);

        return player.ToDto(DateOnly.FromDateTime(_timeProvider.GetUtcNow().Date));
    }
}
