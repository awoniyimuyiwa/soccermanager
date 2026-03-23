using Domain.BackgroundJobs;

namespace Api.Models.V1;

public record BackgroundJobModel(
    Guid Id,
    uint Attempts,
    string? Error,
    uint MaxRetries,
    string? Payload,
    BackgroundJobPriority Priority,
    DateTimeOffset ScheduledFor,
    Guid? SourceId,
    BackgroundJobStatus Status,
    string? TraceId,
    BackgroundJobType Type,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    string? ConcurrencyStamp);
