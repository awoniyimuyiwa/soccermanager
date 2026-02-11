namespace Domain;

public record AuditLogDto(
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
    Guid? UserId) { }

public record FullAuditLogDto(
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
    IReadOnlyCollection<AuditLogActionDto> AuditLogActions,
    IReadOnlyCollection<EntityChangeDto> EntityChanges) : AuditLogDto(
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
