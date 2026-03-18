namespace Domain;

public class BackgroundJob : AuditedEntity, IHasConcurrencyStamp
{
    public uint Attempts { get; set; }

    public string? Error { get; set; }

    public uint MaxRetries { get; set; }

    public required string Payload { get; set; }

    public BackgroundJobPriority Priority { get; set; }

    public DateTimeOffset ScheduledFor { get; set; }

    public BackgroundJobStatus Status { get; set; }

    public BackgroundJobType Type { get; set; }

    public string? ConcurrencyStamp { get; set; }   
}


/// <summary>
/// Defines the processing priority for background jobs.
/// </summary>
/// <remarks>
/// IMPORTANT: Always append new members to the end of the list to maintain 
/// database compatibility and prevent value shifts for existing records.
/// </remarks>
public enum BackgroundJobPriority
{
    Low,
    Normal,
    High,
    Critical
}

/// <summary>
/// Represents the current lifecycle state of a background job.
/// </summary>
/// <remarks>
/// IMPORTANT: Append new members to the end of the list to maintain 
/// database compatibility and prevent value shifts for existing records.
/// </remarks>
public enum BackgroundJobStatus
{
    Queued,
    InProgress,
    Failed
}

/// <summary>
/// Specifies the type of work to be performed by a background job.
/// </summary>
/// <remarks>
/// IMPORTANT: Always append new members to the end of the list to maintain 
/// database compatibility and prevent value shifts for existing records.
/// </remarks>
public enum BackgroundJobType
{
    Email,
    MatchReport,
    ValuationReport
}

