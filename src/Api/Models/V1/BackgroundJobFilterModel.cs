using Api.Attributes;
using Domain.BackgroundJobs;
using System.ComponentModel.DataAnnotations;

namespace Api.Models.V1;

/// <summary>
/// Filter criteria for background jobs.
/// </summary>
/// <param name="CreatedFrom">Jobs created on or after this date (ISO 8601 format).</param>
/// <param name="CreatedTo">Jobs created on or before this date (ISO 8601 format).</param>
/// <param name="Priorities">Filter by specific job priorities (e.g., High, Low).</param>
/// <param name="ScheduledFrom">Jobs scheduled on or after this date (ISO 8601 format).</param>
/// <param name="ScheduledTo">Jobs scheduled on or before this date (ISO 8601 format).</param>
/// <param name="Types">Filter by specific job types or categories.</param>
/// <param name="UpdatedFrom">Jobs updated on or after this date (ISO 8601 format).</param>
/// <param name="UpdatedTo">Jobs updated on or before this date (ISO 8601 format).</param>
public record BackgroundJobFilterModel(
    DateTimeOffset? CreatedFrom = null,
    DateTimeOffset? CreatedTo = null,
    [UniqueMax] BackgroundJobPriority[]? Priorities = null,
    DateTimeOffset? ScheduledFrom = null,
    DateTimeOffset? ScheduledTo = null,
    [UniqueMax] BackgroundJobType[]? Types = null,
    DateTimeOffset? UpdatedFrom = null,
    DateTimeOffset? UpdatedTo = null) : IValidatableObject
{
    public virtual IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (CreatedFrom > CreatedTo)
        {
            yield return new ValidationResult(
                $"{nameof(CreatedFrom)} cannot be later than {nameof(CreatedTo)}.",
                [nameof(CreatedFrom), nameof(CreatedTo)]);
        }

        if (ScheduledFrom > ScheduledTo)
        {
            yield return new ValidationResult(
                $"{nameof(ScheduledFrom)} cannot be later than {nameof(ScheduledTo)}.",
                [nameof(ScheduledFrom), nameof(ScheduledTo)]);
        }

        if (UpdatedFrom > UpdatedTo)
        {
            yield return new ValidationResult(
                $"{nameof(UpdatedFrom)} cannot be later than {nameof(UpdatedTo)}.",
                [nameof(UpdatedFrom), nameof(UpdatedTo)]);
        }
    }
}

/// <summary>
/// Filter criteria for background jobs.
/// </summary>
/// <inheritdoc/>
/// <param name="CreatedFrom"><inheritdoc/></param>
/// <param name="CreatedTo"><inheritdoc/></param>
/// <param name="Priorities"><inheritdoc/></param>
/// <param name="ScheduledFrom"><inheritdoc/></param>
/// <param name="ScheduledTo"><inheritdoc/></param>
/// <param name="Statuses">Specific job statuses to include. If omitted, all statuses are processed.</param>
/// <param name="Types"><inheritdoc/></param>
/// <param name="UpdatedFrom"><inheritdoc/></param>
/// <param name="UpdatedTo"><inheritdoc/></param>
public record GetBackgroundJobFilterModel(
    DateTimeOffset? CreatedFrom = null,
    DateTimeOffset? CreatedTo = null,
    [UniqueMax] BackgroundJobPriority[]? Priorities = null,
    DateTimeOffset? ScheduledFrom = null,
    DateTimeOffset? ScheduledTo = null,
    [UniqueMax] BackgroundJobStatus[]? Statuses = null,
    [UniqueMax] BackgroundJobType[]? Types = null,
    DateTimeOffset? UpdatedFrom = null,
    DateTimeOffset? UpdatedTo = null) : BackgroundJobFilterModel(
        CreatedFrom,
        CreatedTo,
        Priorities,
        ScheduledFrom,
        ScheduledTo,
        Types,
        UpdatedFrom,
        UpdatedTo)
{
    public override IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Reuses the Created/Scheduled/Updated date checks from the base class
        return base.Validate(validationContext);
    }
}


/// <summary>
/// Filter criteria for requeuing background jobs.
/// </summary>
/// <inheritdoc/>
/// <param name="CreatedFrom"><inheritdoc/></param>
/// <param name="CreatedTo"><inheritdoc/></param>
/// <param name="Ids">Specific job IDs to requeue. If empty, all matching jobs are processed.</param>
/// <param name="Priorities"><inheritdoc/></param>
/// <param name="ScheduledFrom"><inheritdoc/></param>
/// <param name="ScheduledTo"><inheritdoc/></param>
/// <param name="SourceIds">Specific source IDs to requeue. If empty, all matching jobs are processed.</param>
/// <param name="TraceIds">Specific trace IDs to requeue. If empty, all matching jobs are processed.</param>
/// <param name="Types"><inheritdoc/></param>
/// <param name="UpdatedFrom"><inheritdoc/></param>
/// <param name="UpdatedTo"><inheritdoc/></param>
public record RequeueBackgroundJobFilterModel(
    DateTimeOffset? CreatedFrom = null,
    DateTimeOffset? CreatedTo = null,
    [UniqueMax(Constants.MaxLengthOfList)] Guid[]? Ids = null,
    [UniqueMax] BackgroundJobPriority[]? Priorities = null,
    DateTimeOffset? ScheduledFrom = null,
    DateTimeOffset? ScheduledTo = null,
    [UniqueMax(Constants.MaxLengthOfList)] Guid[]? SourceIds = null,
    [UniqueMax] BackgroundJobType[]? Types = null,
    [UniqueMax(Constants.MaxLengthOfList)] string[]? TraceIds = null,
    DateTimeOffset? UpdatedFrom = null,
    DateTimeOffset? UpdatedTo = null)
    : BackgroundJobFilterModel(
        CreatedFrom, 
        CreatedTo, 
        Priorities,
        ScheduledFrom, 
        ScheduledTo,
        Types, 
        UpdatedFrom, 
        UpdatedTo)
{
    public override IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Reuses the Created/Scheduled/Updated date checks from the base class
        return base.Validate(validationContext);
    }
}
