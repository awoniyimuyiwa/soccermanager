namespace Application.Contracts;

/// <summary>
/// Base Record for Background Job Handler Data Transfer Objects.
/// </summary>
/// <remarks>
/// <b>Backward Compatibility Rules:</b>
/// <list type="bullet">
/// <item>
/// <description>
/// <b>Only Add, Never Rename:</b> If a field name must change (e.g., UserName to FullName), 
/// add the new property and keep the old one as [Obsolete] to support pending jobs in the queue.
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>Use Optional Properties:</b> Treat new DTO fields as nullable or provide default values 
/// so that legacy jobs missing these fields do not fail deserialization or validation.
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>Avoid Constructor Changes:</b> Adding a required parameter to a primary constructor 
/// is a breaking change. Use property initializers or optional parameters with defaults instead.
/// </description>
/// </item>
/// </list>
/// </remarks>

public abstract record BackgroundJobHandlerDto;

public record EmailBackgroundJobHandlerDto(
    string Body,
    string Subject,
    string To) : BackgroundJobHandlerDto;

public record MatchReportBackgroundJobHandlerDto : BackgroundJobHandlerDto {}

public record ValuationReportBackgroundJobHandlerDto : BackgroundJobHandlerDto {}