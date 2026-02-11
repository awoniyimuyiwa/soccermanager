
namespace Domain;

public class AuditLogAction
{
    public long Id { get; protected set; } = default!;

    public long AuditLogId { get; set; } = default!;

    public DateTimeOffset ExecutionTime { get; set; }

    public string MethodName { get; set; } = null!;

    public string? Parameters { get; set; }

    public string ServiceName { get; set; } = null!;
}

