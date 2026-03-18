namespace Domain;

public record BackgroundJobDto(
    Guid Id,
    uint Attempts,
    string? Error,
    uint MaxRetries,
    string Payload,
    BackgroundJobPriority Priority,
    DateTimeOffset ScheduledFor,
    BackgroundJobStatus Status,
    BackgroundJobType Type,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    string? ConcurrencyStamp = null);

public record BackgroundJobDtoWithInternalId(
    long InternalId,
    Guid Id,
    uint Attempts,
    string? Error,
    uint MaxRetries,
    string Payload,
    BackgroundJobPriority Priority,
    DateTimeOffset ScheduledFor,
    BackgroundJobStatus Status,
    BackgroundJobType Type,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    string? ConcurrencyStamp = null) : BackgroundJobDto(
        Id,
        Attempts, 
        Error,
        MaxRetries, 
        Payload,
        Priority,
        ScheduledFor,
        Status,
        Type,
        CreatedAt,
        UpdatedAt,
        ConcurrencyStamp);

