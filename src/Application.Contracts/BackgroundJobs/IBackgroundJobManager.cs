using Domain.BackgroundJobs;

namespace Application.Contracts.BackgroundJobs;

public interface IBackgroundJobManager 
{ 
    void Enqueue<T>(
        T data,
        uint maxRetries,
        Guid? sourceId,
        BackgroundJobPriority priority = BackgroundJobPriority.Normal,
        DateTimeOffset? scheduledFor = null,
        CancellationToken cancellationToken = default) where T : BackgroundJobHandlerDto; 
}
