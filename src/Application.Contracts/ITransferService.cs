using Domain;

namespace Application.Contracts;

public interface ITransferService
{
    /// <summary>
    /// Pay for transfer: <paramref name="id"/>, move player to destination team owned by user: <paramref name="userId"/>, 
    /// update player value, source team and destination team transfer budget and values.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="userId"></param>
    /// <param name="concurrencyStamp"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>Transfer details</returns>
    /// <exception cref="ConcurrencyException">When concurrency stamp specified does not match the one currently in storage</exception>
    /// <exception cref="DomainException">When a domain rule violation occurs</exception>
    /// <exception cref="EntityNotFoundException">When an entity is not found</exception>
    Task<TransferDto> Pay(
        Guid id,
        long userId,
        string concurrencyStamp,
        CancellationToken cancellationToken = default);
}
