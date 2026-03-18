using Application.Contracts.BackgroundJobs;
using Domain.BackgroundJobs;
using System.Diagnostics;
using System.Text.Json;

namespace Application.BackgroundJobs;

class BackgroundJobManager(
    IBackgroundJobTypeRegistry backgroundJobTypeRegistry,
    IBackgroundJobRepository backgroundJobRepository,
    TimeProvider timeProvider) : IBackgroundJobManager 
{
    readonly IBackgroundJobTypeRegistry _backgroundJobTypeRegistry = backgroundJobTypeRegistry;
    readonly IBackgroundJobRepository _backgroundJobRepository = backgroundJobRepository;
    readonly TimeProvider _timeProvider = timeProvider;

    public void Enqueue<T>(
        T data, 
        uint maxRetries,
        Guid? sourceId = null,
        BackgroundJobPriority priority = BackgroundJobPriority.Normal,
        DateTimeOffset? scheduledFor = null,
        CancellationToken cancellationToken = default) where T : BackgroundJobHandlerDto
    {
        var job = new BackgroundJob
        {
            MaxRetries = maxRetries,
            Payload = JsonSerializer.Serialize(data, data.GetType(), JsonSerializerOptions.Web),
            Priority = priority,
            SourceId = sourceId,
            ScheduledFor = scheduledFor ?? _timeProvider.GetUtcNow(),
            Status = BackgroundJobStatus.Queued,
            TraceId = Activity.Current?.Id,
            Type = _backgroundJobTypeRegistry.GetType(data),
        };

        _backgroundJobRepository.Add(job);
    }

    public async Task<BackgroundJobStatus?> GetStatus(
        Guid sourceId,
        BackgroundJobType type,
        CancellationToken cancellationToken = default)
    {
        var backgroundJob = await _backgroundJobRepository.Get(
            bj => bj.SourceId == sourceId
                  && bj.Type == type,
            cancellationToken);

        return backgroundJob?.Status;
    }

    public Task<int> Requeue(
        RequeueBackgroundJobFilterDto filter,
        CancellationToken cancellationToken = default) => 
        _backgroundJobRepository.RequeueFailed(filter, cancellationToken);
}

