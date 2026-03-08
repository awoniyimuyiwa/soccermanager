using Api.Services;

namespace Api.Models.V1;

public record SessionsModel
{
    public  IEnumerable<UserSessionDto> Sessions { get; init; } = [];
}
