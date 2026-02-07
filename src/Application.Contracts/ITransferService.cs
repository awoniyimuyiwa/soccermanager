using Domain;

namespace Application.Contracts;

public interface ITransferService
{
    /// <summary>
    /// Pay for transfer: <paramref name="id"/>, move player to destination team specified in <paramref name="input"/>, 
    /// update player value, source team value and destination team transfer budget and values.
    /// </summary>
    /// <param name="input">input</param>
    /// <param name="cancellationToken"></param>
    /// <returns>Transfer details</returns>
    /// <exception cref="ConcurrencyException">When concurrency stamp specified does not match the one currently in storage</exception>
    /// <exception cref="DomainException">When a domain rule violation occurs</exception>
    /// <exception cref="EntityNotFoundException">When an entity is not found</exception>
    Task<TransferDto> Pay(
        Guid id,
        PayForTransferDto input,
        CancellationToken cancellationToken = default);
}
