namespace Api.Options;

public class AuditLogOptions
{
    public int CleanupBatchSize { get; set; } = 1000;

    public int RetentionMinutes { get; set; } = 129600; // 90 days default
}
