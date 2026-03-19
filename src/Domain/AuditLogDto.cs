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

public record InternalAuditLogDto(
    long InternalId,
    Guid ExternalId,
    string? BrowserInfo,
    double Duration,
    string? Exception,
    string? HttpMethod,
    string? IpAddress,
    string? RequestId,
    int StatusCode,
    DateTimeOffset TimeStamp,
    string? Url,
    Guid? UserId) : AuditLogDto(
    ExternalId, 
    BrowserInfo, 
    Duration, 
    Exception, 
    HttpMethod,
    IpAddress, 
    RequestId, 
    StatusCode, 
    TimeStamp, 
    Url, 
    UserId);

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

public record AuditLogFilterDto(
    DateTimeOffset? From = null,
    string? HttpMethod = null,
    string? IpAddress = null,
    bool? IsSuccessful = null,
    string? RequestId = null,
    int? StatusCode = null,
    DateTimeOffset? To = null,
    string? Url = null,
    Guid? UserId = null);