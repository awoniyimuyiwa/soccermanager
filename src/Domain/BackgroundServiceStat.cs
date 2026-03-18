namespace Domain;

/// <summary>
/// Should be used by background services to track stats  changes to the system, including user actions and system events. 
/// Doesn't need to extend <see cref="AuditedEntity"/> as their changes should not be audited.
/// </summary>
public class BackgroundServiceStat : Entity, IHasConcurrencyStamp
{
    /// <summary>
    /// JSON containing other details
    /// </summary>
    public string? Details { get; set; }

    public DateTimeOffset LastRunAt { get; set;} = default!;

    public long Total { get; set; }

    public long TotalInLastRun { get; set; }

    public BackgroundServiceStatType Type { get; set; }

    public string? ConcurrencyStamp { get; set; }
}

/// <summary>
/// Identifies the specific background service for health and performance tracking.
/// </summary>
/// <remarks>
/// IMPORTANT: Always append new members to the end of the list to maintain 
/// database compatibility and prevent value shifts for existing records.
/// </remarks>
public enum BackgroundServiceStatType
{
    AuditLogCleanUp,
    BackgroundJob
}
