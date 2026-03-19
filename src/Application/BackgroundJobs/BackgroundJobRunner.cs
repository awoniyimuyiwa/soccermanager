using Application.Contracts;
using Application.Contracts.BackgroundJobs;
using Application.Extensions;
using Domain;
using Domain.BackgroundJobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Application.BackgroundJobs;

public class BackgroundJobRunner(
    IActivityProvider activityProvider,
    IServiceScopeFactory scopeFactory,
    ILogger<BackgroundJobRunner> logger) : IBackgroundJobRunner
{
    readonly IActivityProvider _activityProvider = activityProvider;
    readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    readonly ILogger<BackgroundJobRunner> _logger = logger;
  
    public async Task Run(
        long id, 
        CancellationToken cancellationToken = default)
    {
        // Use a new scope to process job to avoid "poisoned" context
        using var scope = _scopeFactory.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var backgroundJobRepository = scope.ServiceProvider.GetRequiredService<IBackgroundJobRepository>();

        BackgroundJob? backgroundJob;
        try
        {
            backgroundJob = await backgroundJobRepository.Find(
                bj => bj.Id == id,
                true,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch background job {JobId}", id);
            return;
        }

        if (backgroundJob is null) { return; }
    
        using var activity = _activityProvider.StartActivity(   
            $"Process {backgroundJob.Type}",
            ActivityKind.Consumer,
            backgroundJob.TraceId);

        activity?.SetTag("code.namespace", nameof(BackgroundJobRunner));
        activity?.SetTag("messaging.operation", "process");

        activity?.SetTag("job.attempts", backgroundJob.Attempts);
        activity?.SetTag("job.id", backgroundJob.ExternalId);
        activity?.SetTag("job.type", backgroundJob.Type);

        try
        {
            backgroundJob.Status = BackgroundJobStatus.InProgress;
            await unitOfWork.SaveChanges(cancellationToken);

            var handler = scope.ServiceProvider.GetRequiredKeyedService<IBackgroundJobHandler>(backgroundJob.Type);
            
            await unitOfWork.BeginTransaction(cancellationToken);

            await handler!.Handle(
                backgroundJob.ExternalId,
                backgroundJob.Payload,
                cancellationToken);

            backgroundJobRepository.Remove(backgroundJob);

            await unitOfWork.CommitTransaction(cancellationToken);
        }
        catch (Exception exception)
        {
            await unitOfWork.RollbackTransaction(cancellationToken);
            
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);

            // Records the exception as a trace event for better observability.
            activity?.AddException(exception);

            _logger.LogError(exception, "Background job {Id} failed", id);

            await HandleFailure(
                id, 
                exception,
                cancellationToken);
        }
    }

    private async Task HandleFailure(
        long id, 
        Exception exception,
        CancellationToken cancellationToken)
    {
        // Use a new scope to save failure details to avoid "poisoned" context
        using var scope = _scopeFactory.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var backgroundJobRepository = scope.ServiceProvider.GetRequiredService<IBackgroundJobRepository>();
        var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        try
        {
            var backgroundJob = await backgroundJobRepository.Find(
                bj => bj.Id == id,
                true,
                cancellationToken: cancellationToken);
            if (backgroundJob is null) { return; }

            backgroundJob.Attempts++;
            backgroundJob.Error = exception.Trim();

            // Simple exponential backoff; jitter is omitted as jobs are executed sequentially.           
            backgroundJob.ScheduledFor = timeProvider
                .GetUtcNow()
                .AddMinutes(Math.Pow(2, backgroundJob.Attempts));

            backgroundJob.Status = backgroundJob.Attempts >= backgroundJob.MaxRetries
                ? BackgroundJobStatus.Failed : BackgroundJobStatus.Queued;

            await unitOfWork.SaveChanges(cancellationToken);
        }
        catch (Exception criticalException)
        {
            if (_logger.IsEnabled(LogLevel.Critical))
            {
                _logger.LogCritical(
                    criticalException, 
                    "Could not save error state for background job {Id}", id);
            }
        }
    }
}

