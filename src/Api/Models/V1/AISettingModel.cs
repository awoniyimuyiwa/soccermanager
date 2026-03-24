using Domain;

namespace Api.Models.V1;

public record AISettingModel(
    Guid Id,
    string? CustomEndpoint,
    [property: Masked][property: NotAudited] string? Key,
    string Model,
    AIProvider Provider,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
