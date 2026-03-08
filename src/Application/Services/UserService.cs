using Application.Contracts;
using Domain;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace Application.Services;

class UserService(
    IAuditLogManager auditLogManager,
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    [FromKeyedServices(Constants.AuditLogJsonSerializationOptionsName)] JsonSerializerOptions auditJsonSerializerOptions) : BaseService(
        auditLogManager,
        timeProvider,
        auditJsonSerializerOptions), IUserService
{
    readonly IUserRepository _userRepository = userRepository;
    readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<AISettingDto> CreateUpdateAISetting(
        long userId,
        CreateUpdateAISettingDto input,
        CancellationToken cancellationToken = default)
    {
        LogAction(
           new
           {
               userId,
               input
           },
           nameof(CreateUpdateAISetting));

        var user = await _userRepository.Find(
            u => u.Id == userId,
            true,
            [nameof(ApplicationUser.AISetting)],
            cancellationToken) ?? throw new EntityNotFoundException(nameof(ApplicationUser), userId);

        user.AISetting ??= new AISetting
        {
            ExternalId = Guid.NewGuid()
        };

        user.AISetting.CustomEndpoint = input.CustomEndpoint;
        user.AISetting.Key = input.Key;
        user.AISetting.Model = input.Model;
        user.AISetting.Provider = (AIProvider)input.Provider;

        await _unitOfWork.SaveChanges(cancellationToken);

        return user.AISetting.ToDto();
    }
}
