namespace Domain;

public record AuditLogActionDto(
    DateTimeOffset ExecutionTime,
    string MethodName,
    string? Parameters,
    string ServiceName) { }
