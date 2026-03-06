using Domain;

namespace Application.Contracts;

public interface IUserService
{
    Task<AISettingDto> CreateUpdateAISetting(
        long userId, 
        CreateUpdateAISettingDto input,
        CancellationToken cancellationToken = default);
}