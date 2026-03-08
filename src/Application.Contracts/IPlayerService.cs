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

        /// <summary>
        /// Generates a scout report for a player using the specified user's AI settings.
        /// </summary>
        /// <param name="playerId">The unique identifier for the player.</param>
        /// <param name="userId">The unique identifier for the user whose AI settings will be used.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <returns>A string containing the generated scout report.</returns>
        /// <exception cref="EntityNotFoundException">Thrown when the player or user settings cannot be found.</exception>
        Task<string> GetScoutReport(
            Guid playerId,
            long userId,
            CancellationToken cancellationToken = default);
    }
}