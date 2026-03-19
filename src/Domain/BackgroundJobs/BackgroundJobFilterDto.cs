namespace Domain.BackgroundJobs;

public record BackgroundJobFilterDto(
    DateTimeOffset? CreatedFrom,
    DateTimeOffset? CreatedTo,
    BackgroundJobPriority[]? Priorities, // ?Priorities=High&Priorities=Normal
    DateTimeOffset? ScheduledFrom,
    DateTimeOffset? ScheduledTo,
    BackgroundJobType[]? Types,
    DateTimeOffset? UpdatedFrom,
    DateTimeOffset? UpdatedTo)
{
    /// <summary>
    /// Specific job priorities to include. If empty, all priorities are processed.
    /// </summary>
    public BackgroundJobPriority[] Priorities { get; init; } = Priorities ?? [];

    /// <summary>
    /// Specific job types to include. If empty, all types are processed.
    /// </summary>
    public BackgroundJobType[] Types { get; init; } = Types ?? [];
}

public record GetBackgroundJobFilterDto(
    DateTimeOffset? CreatedFrom =  null,
    DateTimeOffset? CreatedTo = null,
    BackgroundJobPriority[]? Priorities = null,
    DateTimeOffset? ScheduledFrom = null,
    DateTimeOffset? ScheduledTo = null,
    BackgroundJobStatus[]? Statuses = null,
    BackgroundJobType[]? Types = null,
    DateTimeOffset? UpdatedFrom = null,
    DateTimeOffset? UpdatedTo = null) : BackgroundJobFilterDto(
        CreatedFrom,
        CreatedTo,
        Priorities,
        ScheduledFrom,
        ScheduledTo,
        Types,
        UpdatedFrom,
        UpdatedTo)
{
    /// <summary>
    /// Specific job statuses to include. If empty, all statuses are processed.
    /// </summary>
    public BackgroundJobStatus[] Statuses { get; init; } = Statuses ?? [];
};

public record RequeueBackgroundJobFilterDto(
    DateTimeOffset? CreatedFrom = null,
    DateTimeOffset? CreatedTo = null,
    Guid[]? Ids = null,
    BackgroundJobPriority[]? Priorities = null,
    DateTimeOffset? ScheduledFrom = null,
    DateTimeOffset? ScheduledTo = null,
    Guid[]? SourceIds = null,
    string[]? TraceIds = null,
    BackgroundJobType[]? Types = null,
    DateTimeOffset? UpdatedFrom = null,
    DateTimeOffset? UpdatedTo = null) : BackgroundJobFilterDto(
        CreatedFrom,
        CreatedTo,
        Priorities,
        ScheduledFrom,
        ScheduledTo,
        Types,
        UpdatedFrom,
        UpdatedTo)
{
    /// <summary>
    /// Specific job IDs to requeue; if null or empty, all matching jobs are processed.
    /// </summary>
    public Guid[] Ids { get; init; } = Ids ?? [];

    /// <summary>
    /// Specific source IDs to requeue; if null or empty, all matching jobs are processed.
    /// </summary>
    public Guid[] SourceIds { get; init; } = SourceIds ?? [];

    /// <summary>
    /// Specific trace IDs to requeue; if null or empty, all matching jobs are processed.
    /// </summary>
    public string[] TraceIds { get; init; } = TraceIds ?? [];
}

