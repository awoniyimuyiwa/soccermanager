namespace Domain;

public record BackgroundJobFilterDto(
    DateTimeOffset? CreatedFrom,
    DateTimeOffset? CreatedTo,
    BackgroundJobPriority[] Priorities, // ?Priorities=High&Priorities=Medium
    DateTimeOffset? ScheduledFrom,
    DateTimeOffset? ScheduledTo,
    BackgroundJobStatus[]? Statuses,
    BackgroundJobType[]? Types,
    DateTimeOffset? UpdatedFrom,
    DateTimeOffset? UpdatedTo)
{
    public BackgroundJobPriority[] Priorities { get; init; } = Priorities ?? [];
    public BackgroundJobStatus[] Statuses { get; init; } = Statuses ?? [];
    public BackgroundJobType[] Types { get; init; } = Types ?? [];
}

