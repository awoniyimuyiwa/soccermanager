using Api.Options;
using Domain;
using Medallion.Threading;
using Microsoft.Extensions.Options;
using System.Threading.Channels;

namespace Api.BackgroundServices;

/// <summary>
/// Removes audit logs older than a calculated cutoff. This process runs automatically 
/// every 24 hours and can also be triggered manually via administrative actions.
/// </summary>
/// <param name="auditLogCleanupReader"></param>
/// <param name="distributedLockProvider"></param>
/// /// <param name="logger">Handles telemetry and error reporting for the cleanup operation.</param>
/// <param name="optionsMonitor">Provides access to configurable retention policies and intervals.</param>
/// <param name="scopeFactory">Used to resolve database contexts within the background task.</param>
/// <param name="timeProvider">The abstraction used to determine the current system time for cutoff logic.</param>
public class AuditLogCleanupService(
    IAuditLogCleanupReader auditLogCleanupReader,
    IDistributedLockProvider distributedLockProvider,
    ILogger<AuditLogCleanupService> logger,
    IOptionsMonitor<AuditLogOptions> optionsMonitor, // Use Monitor for real-time updates
    IServiceScopeFactory scopeFactory,                                             
    TimeProvider timeProvider) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await using var handle = await distributedLockProvider.TryAcquireLockAsync(
                "audit_log_cleanup_lock",
                cancellationToken: cancellationToken);

            if (handle != null)
            {
               await Run(cancellationToken);
            }

            // Create the tasks for the two "Wake Up" conditions
            var timerTask = Task.Delay(TimeSpan.FromHours(24), cancellationToken);
            var manualTriggerTask = auditLogCleanupReader.Reader.WaitToReadAsync(cancellationToken).AsTask();

            // Execution pauses here until ONE of these completes
            await Task.WhenAny(timerTask, manualTriggerTask);

            // If the manual trigger woke the thread up, consume the message to "reset" the door
            if (manualTriggerTask.IsCompletedSuccessfully && manualTriggerTask.Result)
            {
                auditLogCleanupReader.Reader.TryRead(out _);
            }

            // If thread was woken by a trigger, check the distibuted lock again
        }
    }

    public async Task Run(CancellationToken cancellationToken)
    {
        // Read option value inside the loop to get the latest setting
        var options = optionsMonitor.CurrentValue;

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Starting audit log cleanup (Retention: {Minutes} minutes)...", options.RetentionMinutes);
        }

        try
        {
            var cutoff = timeProvider.GetUtcNow().AddMinutes(-options.RetentionMinutes);
            using var scope = scopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var auditLogRepository = scope.ServiceProvider.GetRequiredService<IAuditLogRepository>();
            var backgroundServiceStatRepository = scope.ServiceProvider.GetRequiredService<IBackgroundServiceStatRepository>();
            var deleted = 0;
            int deletedInBatch = 0; 

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
                null,
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
            logger.LogError(ex, "Audit log cleanup failed.");
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