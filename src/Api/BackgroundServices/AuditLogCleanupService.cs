using Api.Options;
using Domain;
using Microsoft.Extensions.Options;

namespace Api.BackgroundServices;

public class AuditLogCleanupService(
    AuditLogCleanupStatus auditCleanupStatus,
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<AuditLogOptions> optionsMonitor, // Use Monitor for real-time updates                                             
    ILogger<AuditLogCleanupService> logger,
    TimeProvider timeProvider)
    : BackgroundService, IAuditLogCleanupTrigger
{
    /// <summary>
    /// Start with 0 available slot for threads
    /// </summary>
    readonly SemaphoreSlim _signal = new(0);

    readonly AuditLogCleanupStatus _status = auditCleanupStatus;

    /// <summary>
    /// Increment available slot for threads on <see cref="_signal"/> by 1. 
    /// </summary>
    public void Trigger() => _signal.Release();

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            // Wait for either the 24hrs or a release
            await _signal.WaitAsync(TimeSpan.FromHours(24), cancellationToken);

            // After timeout or release, if available slot is still more than 0 due to Release being called rapidly,
            // quickly drain available slot back to 0 by using WaitAsync() to decrement by 1 until available slot is 0,
            // to limit concurrent clean up processes.
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

