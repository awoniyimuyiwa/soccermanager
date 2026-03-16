using Api.Options;
using Application.Contracts;
using Domain;
using Medallion.Threading;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Registry;
using System.Threading.Channels;

namespace Api.BackgroundServices;

/// <summary>
/// A hosted worker that orchestrates the execution of background jobs.
/// </summary>
/// <remarks>
/// This service listens for triggers via <see cref="IBackgroundJobReader"/> (e.g., scheduled timers or manual signals).
/// It ensures high availability and consistency by using <see cref="IDistributedLockProvider"/> to prevent 
/// concurrent execution across multiple instances.
/// </remarks>
/// <param name="backgroundJobReader">Provides a signaling mechanism to trigger job processing.</param>
/// <param name="distributedLockProvider">Ensures mutual exclusion across distributed nodes.</param>
/// <param name="logger">Handles telemetry, audit trails, and error reporting.</param>
/// <param name="optionsMonitor"></param>
/// <param name="scopeFactory">Creates dependency scopes for database and handler operations.</param>
/// <param name="resiliencePipelineProvider">Applies transient fault handling and retry policies.</param>
/// <param name="timeProvider">An abstraction for system time used to calculate expiration thresholds.</param>
public class BackgroundJobService(
    IBackgroundJobReader backgroundJobReader,
    IDistributedLockProvider distributedLockProvider,
    ILogger<BackgroundJobService> logger,
    IOptionsMonitor<BackgroundJobOptions> optionsMonitor,
    IServiceScopeFactory scopeFactory,
    ResiliencePipelineProvider<string> resiliencePipelineProvider,
    TimeProvider timeProvider) : BackgroundService
{
    readonly ResiliencePipeline<IDistributedSynchronizationHandle?> _resiliencePipeline = resiliencePipelineProvider.GetPipeline<IDistributedSynchronizationHandle?>(Constants.DistributedLockResiliencePolicyName);

    protected override async Task ExecuteAsync(CancellationToken serviceCancellationToken)
    {
        var options = optionsMonitor.CurrentValue;
        var interval = TimeSpan.FromSeconds(options.PollingIntervalSeconds);
        using PeriodicTimer timer = new(interval);

        while (!serviceCancellationToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();

                // Create the tasks for the two "Wake Up" conditions
                var timerTask = timer.WaitForNextTickAsync(serviceCancellationToken).AsTask();
                var manualTriggerTask = backgroundJobReader.Reader.WaitToReadAsync(serviceCancellationToken).AsTask();

                // Execution pauses here until ONE of these completes
                var completed = await Task.WhenAny(timerTask, manualTriggerTask);

                // If the manual trigger woke the thread up, consume the message to "reset" the door
                if (completed == manualTriggerTask && await manualTriggerTask)
                {
                    backgroundJobReader.Reader.TryRead(out _);
                }

                // If thread was woken by a trigger, check the distributed lock again
                await using var handle = await _resiliencePipeline.ExecuteAsync(async resilienceCancellationToken =>
                {
                    return await distributedLockProvider.TryAcquireLockAsync(
                        "background_job_service_lock",
                        cancellationToken: resilienceCancellationToken);
                }, serviceCancellationToken);

                if (handle is not null)
                {
                    await Run(
                        scope.ServiceProvider, 
                        serviceCancellationToken);
                }
            }
            catch (OperationCanceledException) 
            {
                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Background job processing service is shutting down.");
                }
                break; 
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Background job processing service crashed! Restarting in 10 seconds...");

                // Wait to avoid high CPU crash loop
                await Task.Delay(TimeSpan.FromSeconds(options.PollingIntervalSeconds), serviceCancellationToken);
            }
        }
    }

    public async Task Run(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Starting background job processing");
        }

        var unitOfWork = serviceProvider.GetRequiredService<IUnitOfWork>();
        var backgroundServiceStatRepository = serviceProvider.GetRequiredService<IBackgroundServiceStatRepository>();
        var processed = 0;

        try
        {
            var options = optionsMonitor.CurrentValue;
            var backgroundJobRepository = serviceProvider.GetRequiredService<IBackgroundJobRepository>();
            var backgroundJobRunner = serviceProvider.GetRequiredService<IBackgroundJobRunner>();

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Requeuing stuck jobs");
            }
            var requeuedCount = await backgroundJobRepository.RequeueStuck(
                [],
                (uint)options.StuckJobThresholdMinutes,
                cancellationToken);
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Requeued jobs: {requeuedCount}", requeuedCount);
            }

            var processedInBatch = 0;

            do
            {
                processedInBatch = 0;

                var ids = await backgroundJobRepository.GetIds(
                    bj => bj.Status == BackgroundJobStatus.Queued
                          && bj.ScheduledFor <= timeProvider.GetUtcNow(),
                    options.BatchSize,
                    cancellationToken);

                foreach (var id in ids)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    // Run() handles its own internal scoping
                    await backgroundJobRunner.Run(id, cancellationToken);

                    processedInBatch++;
                }

                if (processedInBatch > 0)
                {
                    processed += processedInBatch;
                    // Send signalr notification or server sent event to the UI here if needed
                }

                await Task.Delay(100, cancellationToken); // Small delay to prevent overloading the database
            } while (processedInBatch > 0 && !cancellationToken.IsCancellationRequested);

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Background job processing succeeded (Background jobs processed in current run: {processed}.", processed);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Background job processing failed!");
        }
        finally
        {
            await backgroundServiceStatRepository.AddOrUpdate(
                null,
                processed,
                BackgroundServiceStatType.BackgroundJob,
                cancellationToken);

            await unitOfWork.SaveChanges(cancellationToken);
        }
    }
}

public interface IBackgroundJobTrigger
{
    /// <summary>
    /// Signals the background worker to process the queue.
    /// </summary>
    /// <example>
    /// <code>
    /// _backgroundJobRepository.Add(new BackgroundJob
    /// {
    ///     ExternalId = Guid.NewGuid(),
    ///     MaxRetries = 3,
    ///     Payload = JsonSerializer.Serialize(new EmailBackgroundJobHandlerDto("Body", "Sub", "you@domain.com"), JsonSerializerOptions.Web),
    ///     Priority = BackgroundJobPriority.Normal,
    ///     ScheduledFor = DateTimeOffset.UtcNow
    ///     Type = BackgroundJobType.Email
    /// });
    /// await _uow.SaveChanges();
    /// _backgroundJobTrigger.Trigger();
    /// </code>
    /// </example>
    void Trigger();
}

public interface IBackgroundJobReader
{
    ChannelReader<bool> Reader { get; }
}

/// <summary>
/// Provides a throttled signaling mechanism to coordinate background job processing tasks.
/// Uses a bounded channel to ensure that redundant trigger requests are dropped 
/// if a processing operation is already queued.
/// </summary>
public class BackgroundJobTrigger : IBackgroundJobTrigger, IBackgroundJobReader
{
    // Bounded to 1 to ensure we only ever have one "pending" work signal.
    private readonly Channel<bool> _channel = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
    {
        FullMode = BoundedChannelFullMode.DropWrite
    });

    /// <summary>
    /// Gets the reader used by the background service to listen for process signals.
    /// </summary>
    public ChannelReader<bool> Reader => _channel.Reader;

    /// <summary>
    /// Asynchronously signals that a background job processing should be performed.
    /// If a signal is already pending, this call returns immediately without queuing additional work.
    /// </summary>
    public void Trigger()
    {
        // Thread-safe "Fire and Forget" signal
        _channel.Writer.TryWrite(true);
    }
}