using Domain;
using Microsoft.Extensions.AI;

namespace Application.Contracts;

public interface IChatClientFactory
{
    /// <summary>
    /// Resolves and initializes a provider-specific chat client for a given user.
    /// </summary>
    /// <param name="userId">The unique identifier of the user requesting the client.</param>
    /// <param name="cancellationToken">Propagates notification that operations should be cancelled.</param>
    /// <returns>
    /// A configured <see cref="IChatClient"/> implementation; 
    /// </returns>
    /// <exception cref="DomainException">When user has not configured AISetting</exception>
    /// <exception cref="EntityNotFoundException">Thrown when user is not found.</exception>
    Task<IChatClient> Create(
        long userId, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies the AI provider credentials and model availability by performing a lightweight 
    /// "ping" (e.g., a metadata or model retrieval call)
    /// </summary>
    /// <param name="setting">The AI settings containing the provider, model, and unprotected API key.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous verification operation.</returns>
    /// <exception cref="DomainException">Thrown if authentication fails, the model is inaccessible, or the endpoint is unreachable.</exception>
    Task Verify(
        AISettingDto setting,
        CancellationToken cancellationToken = default);
}

