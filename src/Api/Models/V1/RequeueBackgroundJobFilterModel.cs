using Api.Attributes;
using Domain.BackgroundJobs;

namespace Api.Models.V1;

public record RequeueBackgroundJobFilterModel(
    DateTimeOffset? CreatedFrom = null,
    DateTimeOffset? CreatedTo = null,
    [property: UniqueMax(100)] Guid[]? Ids = null,
    BackgroundJobPriority[]? Priorities = null,
    DateTimeOffset? ScheduledFrom = null,
    DateTimeOffset? ScheduledTo = null,
    [property: UniqueMax(100)] Guid[]? SourceIds = null,
    [property: UniqueMax(100)] string[]? TraceIds = null,
    BackgroundJobType[]? Types = null,
    DateTimeOffset? UpdatedFrom = null,
    DateTimeOffset? UpdatedTo = null)
    : BackgroundJobFilterDto(
        CreatedFrom,
        CreatedTo,
        Priorities,
        ScheduledFrom,
        ScheduledTo,
        Types,
        UpdatedFrom,
        UpdatedTo) {}



