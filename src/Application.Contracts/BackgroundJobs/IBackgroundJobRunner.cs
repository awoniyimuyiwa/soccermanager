namespace Application.Contracts.BackgroundJobs;

public interface IBackgroundJobRunner
{
    Task Run(
        long id,
        CancellationToken cancellationToken = default);
}
