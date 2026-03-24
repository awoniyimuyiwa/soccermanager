namespace Api.Models.V1;

public record UserSessionModel(
    string Id,
    string? AuthScheme,
    string? CorrelationId,
    string DeviceInfo,
    string? IpAddress,
    DateTimeOffset LastSeen,
    string Location,
    DateTimeOffset LoginTime);
