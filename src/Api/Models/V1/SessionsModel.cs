namespace Api.Models.V1;

public record SessionsModel
{
    public  IEnumerable<UserSessionModel> Sessions { get; init; } = [];
}
