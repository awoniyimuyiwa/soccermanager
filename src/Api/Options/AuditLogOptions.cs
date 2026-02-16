using System.ComponentModel.DataAnnotations;

namespace Api.Options;

public class AuditLogOptions
{
    public const string SectionName = "AuditLogOptions";

    [Range(100, 1000)]
    public int CleanupBatchSize { get; set; } = 1000;

    [Range(1, 129600)]
    public int RetentionMinutes { get; set; } = 129600; // 90 days default
}

