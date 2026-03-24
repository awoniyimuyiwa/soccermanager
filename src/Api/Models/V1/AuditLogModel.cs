using Domain;

namespace Api.Models.V1;

public record AuditLogModel(
    Guid Id,
    string? BrowserInfo,
    double Duration,
    string? Exception,
    string? HttpMethod,
    string? IpAddress,
    string? RequestId,
    int StatusCode,
    DateTimeOffset Timestamp,
    string? Url,
    Guid? UserId);

public record AuditLogActionModel(
    DateTimeOffset ExecutionTime,
    string MethodName,
    string? Parameters,
    string ServiceName);

public record EntityChangeModel(
    string EntityName,
    string? NewValues,
    string? OldValues,
    EntityChangeType Type);

public record FullAuditLogModel(
    Guid Id,
    string? BrowserInfo,
    double Duration,
    string? Exception,
    string? HttpMethod,
    string? IpAddress,
    string? RequestId,
    int StatusCode,
    DateTimeOffset Timestamp,
    string? Url,
    Guid? UserId,
    IReadOnlyCollection<AuditLogActionModel> AuditLogActions,
    IReadOnlyCollection<EntityChangeModel> EntityChanges)
    : AuditLogModel(
        Id, 
        BrowserInfo, 
        Duration, 
        Exception, 
        HttpMethod, 
        IpAddress, 
        RequestId, 
        StatusCode, 
        Timestamp, 
        Url, 
        UserId);
