using Application.Contracts;
using Domain;

namespace Application.Services
{
    public interface IPlayerService
    {
        /// <summary>
        /// Place player on transfer list
        /// </summary>
        /// <param name="playerId"></param>
        /// <param name="userId"></param>
        /// <param name="input"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>Transfer details</returns>
        /// <exception cref="ConcurrencyException">When concurrency stamp specified does not match the one currently in storage</exception>
        /// <exception cref="DomainException">When a domain rule violation occurs</exception>
        /// <exception cref="EntityNotFoundException">When an entity is not found</exception>
        Task<TransferDto> PlaceOnTransferList(
            Guid playerId,
            long userId,
            PlaceOnTransferListDto input,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Update player
        /// </summary>
        /// <param name="playerId"></param>
        /// <param name="userId"></param>
        /// <param name="input"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>Player details</returns>
        /// <exception cref="ConcurrencyException">When concurrency stamp specified does not match the one currently in storage</exception>
        /// <exception cref="EntityNotFoundException">When an entity is not found</exception>
        Task<PlayerDto> Update(
            Guid playerId,
            long userId,
            UpdatePlayerDto input, 
            CancellationToken cancellationToken = default);
    }
}