using Api.Options;
using Domain;
using Medallion.Threading;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Registry;
using System.Text.Json;
using System.Threading.Channels;

namespace Api.BackgroundServices;

/// <summary>
/// Deletes expired audit logs based on configured retention policies.
/// Runs automatically at 02:00 UTC or via manual trigger.
/// </summary>
/// <param name="auditLogCleanupReader">Accesses logs for cleanup.</param>
/// <param name="distributedLockProvider">Prevents concurrent cleanup executions.</param>
/// <param name="logger">Telemetry and error logger.</param>
/// <param name="optionsMonitor">Configuration settings for retention.</param>
/// <param name="scopeFactory">Service scope factory for DB operations.</param>
/// <param name="timeProvider">System time abstraction.</param>
/// <param name="resiliencePipelineProvider">Retry and resilience logic provider.</param>
public class AuditLogCleanupService(
    IAuditLogCleanupReader auditLogCleanupReader,
    IDistributedLockProvider distributedLockProvider,
    ILogger<AuditLogCleanupService> logger,
    IOptionsMonitor<AuditLogOptions> optionsMonitor, // Use Monitor for real-time updates
    IServiceScopeFactory scopeFactory,
    ResiliencePipelineProvider<string> resiliencePipelineProvider,
    TimeProvider timeProvider) : BackgroundService
{
    readonly ResiliencePipeline<IDistributedSynchronizationHandle?> _resiliencePipeline = resiliencePipelineProvider.GetPipeline<IDistributedSynchronizationHandle?>(Constants.DistributedLockResiliencePolicy);
    
    protected override async Task ExecuteAsync(CancellationToken serviceCancellationToken)
    {
        while (!serviceCancellationToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();               
                var backgroundServiceStatRepository = scope.ServiceProvider.GetRequiredService<IBackgroundServiceStatRepository>();

                var lastRun = (await backgroundServiceStatRepository.Get(bss => bss.Type == BackgroundServiceStatType.AuditLogCleanUp, serviceCancellationToken))?.LastRunAt.DateTime;
                var now = timeProvider.GetUtcNow();
                var scheduledToday = now.Date.AddHours(2); // 2AM everyday
                var shouldRunImmediately = lastRun == null || (lastRun < scheduledToday && now >= scheduledToday);

                if (!shouldRunImmediately)
                {
                    // Create the tasks for the two "Wake Up" conditions
                    var nextRunTime = now >= scheduledToday ? scheduledToday.AddDays(1) : scheduledToday;
                    var timerTask = Task.Delay(nextRunTime - now, serviceCancellationToken);
                    var manualTriggerTask = auditLogCleanupReader.Reader.WaitToReadAsync(serviceCancellationToken).AsTask();

                    // Execution pauses here until ONE of these completes
                    var completed = await Task.WhenAny(timerTask, manualTriggerTask);
                   
                    // If the manual trigger woke the thread up, consume the message to "reset" the door
                    if (completed == manualTriggerTask && manualTriggerTask.Result)
                    {
                        auditLogCleanupReader.Reader.TryRead(out _);
                    }
                       
                    // If thread was woken by a trigger, check the distibuted lock again
                }

                await using var handle = await _resiliencePipeline.ExecuteAsync(async resilienceCancellationToken =>
                {
                    // No TTL, hold the lock and lock as long the service is alive
                    return await distributedLockProvider.TryAcquireLockAsync(
                        "audit_log_cleanup_lock",
                        cancellationToken: resilienceCancellationToken);
                }, serviceCancellationToken);

                if (handle is not null)
                {
                    await Run(
                        scope.ServiceProvider, 
                        backgroundServiceStatRepository, 
                        serviceCancellationToken);   
                }
                else if (shouldRunImmediately) 
                {
                    // Small safety delay if we were in a 'shouldRunImmediately' state (e.g it's 3AM but he 2AM run hasn't happened
                    await Task.Delay(TimeSpan.FromMinutes(1), serviceCancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Audit log clean up background service is shutting down.");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Audit log clean up background service crashed! Restarting in 10 seconds...");
                
                // Wait to avoid high CPU crash loop
                await Task.Delay(TimeSpan.FromSeconds(10), serviceCancellationToken);
            }
        }
    }

    public async Task Run(
        IServiceProvider serviceProvider,
        IBackgroundServiceStatRepository backgroundServiceStatRepository,
        CancellationToken cancellationToken)
    {
        try
        {
            // Read option value inside the loop to get the latest setting
            var options = optionsMonitor.CurrentValue;

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Starting audit log cleanup (Retention: {Minutes} minutes)...", options.RetentionMinutes);
            }
           
            var unitOfWork = serviceProvider.GetRequiredService<IUnitOfWork>();
            var auditLogRepository = serviceProvider.GetRequiredService<IAuditLogRepository>();
            var deleted = 0;
            var deletedInBatch = 0;
            var cutoff = timeProvider.GetUtcNow().AddMinutes(-options.RetentionMinutes);
            await unitOfWork.BeginTransaction(cancellationToken);
            
            do
            {
                deletedInBatch = await auditLogRepository.ExecuteDelete(
                    al => al.TimeStamp < cutoff,
                    options.CleanupBatchSize,
                    cancellationToken);
                if (deletedInBatch > 0)
                {
                    deleted += deletedInBatch;
                    // Send signalr notification or server sent event to the UI here if needed
                }

                await Task.Delay(100, cancellationToken); // Small delay to prevent overloading the database
            } while (deletedInBatch > 0 && !cancellationToken.IsCancellationRequested);

            await backgroundServiceStatRepository.AddOrUpdate(
                JsonSerializer.Serialize(
                    new
                    {
                        cutoff
                    }),
                deleted,
                BackgroundServiceStatType.AuditLogCleanUp,
                cancellationToken);
            await unitOfWork.CommitTransaction(cancellationToken); // Commit or rollback

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Audit log cleanup succeeded (Audit logs deleted in current run: {Deleted}.", deleted);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Audit log clean up failed!");
        }
    }
}

public interface IAuditLogCleanupTrigger
{
    void Trigger();
}

public interface IAuditLogCleanupReader
{
    ChannelReader<bool> Reader { get; }
}

/// <summary>
/// Provides a throttled signaling mechanism to coordinate audit log maintenance tasks.
/// Uses a bounded channel to ensure that redundant trigger requests are dropped 
/// if a cleanup operation is already queued.
/// </summary>
public class AuditLogCleanupTrigger : IAuditLogCleanupTrigger, IAuditLogCleanupReader
{
    // Bounded to 1 to ensure we only ever have one "pending" work signal.
    private readonly Channel<bool> _channel = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
    {
        FullMode = BoundedChannelFullMode.DropWrite
    });

    /// <summary>
    /// Gets the reader used by the background service to listen for cleanup signals.
    /// </summary>
    public ChannelReader<bool> Reader => _channel.Reader;

    /// <summary>
    /// Asynchronously signals that a cleanup operation should be performed.
    /// If a signal is already pending, this call returns immediately without queuing additional work.
    /// </summary>
    public void Trigger()
    {
        // Thread-safe "Fire and Forget" signal
        _channel.Writer.TryWrite(true);
    }
}