using Domain.BackgroundJobs;

namespace Application.Contracts.BackgroundJobs;

public interface IBackgroundJobTypeRegistry
{
    BackgroundJobType GetType(BackgroundJobHandlerDto dto);
}
