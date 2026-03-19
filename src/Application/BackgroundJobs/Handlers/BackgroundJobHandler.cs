using Application.Contracts.BackgroundJobs;
using System.Text.Json;

namespace Application.BackgroundJobs.Handlers;

abstract class BackgroundJobHandler<T> : IBackgroundJobHandler where T : BackgroundJobHandlerDto
{
    public async Task Handle(
        Guid id,
        string payload,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            throw new InvalidOperationException("Job payload is missing.");
        }

        var data = JsonSerializer.Deserialize<T>(payload, JsonSerializerOptions.Web) 
            ?? throw new InvalidOperationException("Job payload is malformed.");

        await Handle(
            id, 
            data, 
            cancellationToken);
    }

    protected abstract Task Handle(
        Guid id,
        T data,
        CancellationToken cancellationToken);
}
