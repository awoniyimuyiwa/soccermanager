using Api.Options;
using Domain;
using Microsoft.Extensions.Options;

namespace Api.BackgroundServices;

/// <summary>
/// Removes audit logs older than a calculated cutoff. This process runs automatically 
/// every 24 hours and can also be triggered manually via administrative actions.
/// </summary>
/// <param name="auditCleanupStatus">Tracks the state and last run time of the cleanup process.</param>
/// <param name="scopeFactory">Used to resolve database contexts within the background task.</param>
/// <param name="optionsMonitor">Provides access to configurable retention policies and intervals.</param>
/// <param name="logger">Handles telemetry and error reporting for the cleanup operation.</param>
/// <param name="timeProvider">The abstraction used to determine the current system time for cutoff logic.</param>
/// <remarks>
/// <para>
/// <b>Scaling Note:</b> This implementation does not natively support horizontal scaling. 
/// To prevent concurrent execution across multiple instances, this service should be:
/// <list type="bullet">
/// <item><description>Isolated to a single-instance background project.</description></item>
/// <item><description>Or protected by a distributed lock (e.g., Redis, SQL, or Zookeeper).</description></item>
/// </list>
/// </para>
/// </remarks>
public class AuditLogCleanupService(
    AuditLogCleanupStatus auditCleanupStatus,
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<AuditLogOptions> optionsMonitor, // Use Monitor for real-time updates                                             
    ILogger<AuditLogCleanupService> logger,
    TimeProvider timeProvider)
    : BackgroundService, IAuditLogCleanupTrigger
{
    /// <summary>
    /// Initializes the semaphore with zero available slots to block the background worker 
    /// until a manual or scheduled signal is received.
    /// </summary>
    readonly SemaphoreSlim _signal = new(0);

    readonly AuditLogCleanupStatus _status = auditCleanupStatus;

    /// <summary>
    /// Signals the background worker to begin a cleanup cycle by releasing a slot in the <see cref="_signal"/> semaphore.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Thread Safety Note:</b> While <see cref="ExecuteAsync"/> runs on a single background thread, 
    /// this method can be invoked concurrently by multiple web request threads. 
    /// </para>
    /// <para>
    /// To ensure thread safety, clean up execution and access to shared state like <see cref="_status"/> is synchronized using <see cref="SemaphoreSlim"/>. 
    /// This prevents race conditions when multiple manuals triggers and the scheduled 24-hour interval overlap.
    /// </para>
    /// </remarks>
    public void Trigger() => _signal.Release();

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            // Wait for either 24hrs or a release
            await _signal.WaitAsync(TimeSpan.FromHours(24), cancellationToken);

            // After timeout or release signal is received, drain any additional slots in the semaphore.
            // This prevents "burst" triggers (e.g., multiple rapid manual requests) from 
            // queuing up multiple back-to-back cleanup cycles. By decrementing the count to 0, 
            // we ensure that only one cleanup operation proceeds and subsequent redundant 
            // signals are cleared.
            while (_signal.CurrentCount > 0)
            {              
                await _signal.WaitAsync(cancellationToken);
            }

            // Read option value inside the loop to get the latest setting
            var options = optionsMonitor.CurrentValue;

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Starting audit cleanup (Retention: {Minutes} minutes)...", options.RetentionMinutes);
            }

            try
            {
                var cutoff = timeProvider.GetUtcNow().AddMinutes(-options.RetentionMinutes);
                await Run(
                   options,
                   cutoff,
                   cancellationToken);

                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Audit cleanup succeeded (Deleted in current run: {DeletedInCurrentRun}) (Total deleted: {TotalDeleted}).",
                        _status.DeletedInCurrentRun, 
                        _status.TotalDeleted);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Audit cleanup failed.");
            }
        }
    }

    public async Task Run(
        AuditLogOptions options, 
        DateTimeOffset cutoff,
        CancellationToken cancellationToken)
    {
        _status.IsRunning = true;
        _status.ResetCurrentRun();

        try
        {
            int deleted = 0;
            using var scope = scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IAuditLogRepository>();

            do
            {
                deleted = await repository.ExecuteDelete(
                    al => al.TimeStamp < cutoff,
                    options.CleanupBatchSize,
                    cancellationToken);
                if (deleted > 0)
                {
                    _status.Increment(deleted);
                    // Send signalr notification or server sent event to the UI here if needed
                }

                await Task.Delay(100, cancellationToken); // Small delay to prevent overloading the database
            } while (deleted > 0 && !cancellationToken.IsCancellationRequested);
        }
        finally
        {
            _status.IsRunning = false;
            _status.LastRun = timeProvider.GetUtcNow();
        }
    }
}

public interface IAuditLogCleanupTrigger
{
    /// <summary>
    /// Wakeup the cleanup service to run immediately
    /// </summary>
    void Trigger();
}

public class AuditLogCleanupStatus
{
    private int _deletedInCurrentRun;

    private int _totalDeleted;

    public bool IsRunning { get; internal set; }

    public DateTimeOffset? LastRun { get; internal set; }

    public int DeletedInCurrentRun => _deletedInCurrentRun;

    public int TotalDeleted => _totalDeleted;

    public void Increment(int count)
    {
        Interlocked.Add(ref _deletedInCurrentRun, count);
        Interlocked.Add(ref _totalDeleted, count);
    }

    public void ResetCurrentRun() => Interlocked.Exchange(ref _deletedInCurrentRun, 0);
}

