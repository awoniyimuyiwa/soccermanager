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
/// A hosted worker that orchestrates the deletion of expired audit logs based on retention policies.
/// </summary>
/// <remarks>
/// This service is triggered via <see cref="IAuditLogCleanupReader"/> signals, typically scheduled 
/// for 02:00 UTC or manually initiated. It utilizes <see cref="IDistributedLockProvider"/> to ensure 
/// only one cleanup instance runs across a distributed environment.
/// </remarks>
/// <param name="auditLogCleanupReader">Provides a signaling mechanism to trigger the cleanup process.</param>
/// <param name="distributedLockProvider">Ensures mutual exclusion to prevent concurrent cleanup executions.</param>
/// <param name="logger">Handles telemetry, audit trails, and error reporting.</param>
/// <param name="optionsMonitor">Provides access to dynamic configuration for log retention settings.</param>
/// <param name="scopeFactory">Creates dependency scopes for database and cleanup operations.</param>
/// <param name="resiliencePipelineProvider">Applies transient fault handling and retry policies for database operations.</param>
/// <param name="timeProvider">An abstraction for system time used to calculate expiration thresholds.</param>
public class AuditLogCleanupService(
    IAuditLogCleanupReader auditLogCleanupReader,
    IDistributedLockProvider distributedLockProvider,
    ILogger<AuditLogCleanupService> logger,
    IOptionsMonitor<AuditLogOptions> optionsMonitor, // Use Monitor for real-time updates
    IServiceScopeFactory scopeFactory,
    ResiliencePipelineProvider<string> resiliencePipelineProvider,
    TimeProvider timeProvider) : BackgroundService
{
    readonly ResiliencePipeline<IDistributedSynchronizationHandle?> _resiliencePipeline = resiliencePipelineProvider.GetPipeline<IDistributedSynchronizationHandle?>(Constants.DistributedLockResiliencePolicyName);
    
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
                    if (completed == manualTriggerTask && await manualTriggerTask)
                    {
                        auditLogCleanupReader.Reader.TryRead(out _);
                    }
                       
                    // If thread was woken by a trigger, check the distributed lock again
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
                    // Small safety delay if we were in a 'shouldRunImmediately' state (e.g it's 3AM but the 2AM run hasn't happened)
                    await Task.Delay(TimeSpan.FromMinutes(1), serviceCancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Audit log clean up background service is shutting down.");
                }
                break;
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

            // Resolve scoped dependencies required for the current execution cycle.
            var unitOfWork = serviceProvider.GetRequiredService<IUnitOfWork>();
            var auditLogRepository = serviceProvider.GetRequiredService<IAuditLogRepository>();

            var deleted = 0;
            var deletedInBatch = 0;
            var cutoff = timeProvider.GetUtcNow().AddMinutes(-options.RetentionMinutes);

            await unitOfWork.BeginTransaction(cancellationToken);
            
            do
            {
                deletedInBatch = await auditLogRepository.ExecuteDelete(
                    al => al.CreatedAt < cutoff,
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
                    },
                    JsonSerializerOptions.Web),
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