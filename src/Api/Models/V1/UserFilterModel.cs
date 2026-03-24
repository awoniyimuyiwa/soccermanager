using System.ComponentModel.DataAnnotations;

namespace Api.Models.V1;

/// <summary>
/// Filter criteria for users.
/// </summary>
/// <param name="Search">A term to search by name or email.</param>
/// <param name="CreatedFrom">Users created on or after this date (ISO 8601 format).</param>
/// <param name="CreatedTo">Users created on or before this date (ISO 8601 format).</param>
/// <param name="IsEmailConfirmed">Filter by email confirmation status.</param>
/// <param name="UpdatedFrom">Users updated on or after this date (ISO 8601 format).</param>
/// <param name="UpdatedTo">Users updated on or before this date (ISO 8601 format).</param>
public record UserFilterModel(
    [MaxLength(Domain.Constants.StringMaxLength)] string? Search = null,
    DateTimeOffset? CreatedFrom = null,
    DateTimeOffset? CreatedTo = null,
    bool? IsEmailConfirmed = null,
    DateTimeOffset? UpdatedFrom = null,
    DateTimeOffset? UpdatedTo = null) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (CreatedFrom > CreatedTo)
        {
            yield return new ValidationResult(
                $"{nameof(CreatedFrom)} cannot be later than {nameof(CreatedTo)}.",
                [nameof(CreatedFrom), nameof(CreatedTo)]);
        }

        if (UpdatedFrom > UpdatedTo)
        {
            yield return new ValidationResult(
                $"{nameof(UpdatedFrom)} cannot be later than {nameof(UpdatedTo)}.",
                [nameof(UpdatedFrom), nameof(UpdatedTo)]);
        }
    }
}