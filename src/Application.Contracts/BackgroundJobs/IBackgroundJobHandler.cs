namespace Application.Contracts.BackgroundJobs;

public interface IBackgroundJobHandler
{
    Task Handle(
        Guid id,
        string payload,
        CancellationToken cancellationToken = default);
}
