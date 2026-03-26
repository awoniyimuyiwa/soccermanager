using System.ComponentModel.DataAnnotations;

namespace Api.Options;

public class IdempotencyOptions
{
    public const string SectionName = "IdempotencyOptions";

    /// <summary>
    /// Global default for the execution lock duration in seconds.
    /// This should be slightly longer than Request Timeout to include a safety buffer.
    /// Default is 65 seconds (60s Work + 5s Buffer).
    /// </summary>
    [Range(1, 300, ErrorMessage = "Lock TTL must be between 1 and 300 seconds.")]
    public int LockTTLSeconds { get; set; } = 65;

    /// <summary>
    /// Global default for how long to cache the response.
    /// Default is 1440 (24 hours).
    /// </summary>
    [Range(1, 10080, ErrorMessage = "Record TTL must be between 1 minute and 7 days.")]
    public int RecordTTLMinutes { get; set; } = 1440; // 24 hours
}

