namespace Domain;

public record AISettingDto(
    Guid Id,
    string? CustomEndpoint,
    [property: Masked] [property: NotAudited] string? Key,
    string Model,
    AIProvider Provider,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

