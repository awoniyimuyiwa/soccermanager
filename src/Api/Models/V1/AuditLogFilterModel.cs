namespace Api.Models.V1;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Filter criteria for audit logs.
/// </summary>
/// <param name="From">Logs created on or after this date (ISO 8601 format).</param>
/// <param name="HttpMethod"></param>
/// <param name="IpAddress"></param>
/// <param name="IsSuccessful"></param>
/// <param name="RequestId"></param>
/// <param name="StatusCode"></param>
/// <param name="To">Logs created on or before this date (ISO 8601 format).</param>
/// <param name="Url"></param>
/// <param name="UserId"></param>
public record AuditLogFilterModel(
    DateTimeOffset? From = null,
    [MaxLength(Domain.Constants.StringMaxLength)] string? HttpMethod = null,
    [MaxLength(Domain.Constants.StringMaxLength)] string? IpAddress = null,
    bool? IsSuccessful = null,
    [MaxLength(Domain.Constants.StringMaxLength)] string? RequestId = null,
    int? StatusCode = null,
    DateTimeOffset? To = null,
    [MaxLength(Domain.Constants.StringMaxLength)] string? Url = null,
    Guid? UserId = null) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (From.HasValue && To.HasValue && From > To)
        {
            yield return new ValidationResult(
                $"{nameof(From)} cannot be later than {nameof(To)}.",
                [nameof(From), nameof(To)]);
        }
    }
}
