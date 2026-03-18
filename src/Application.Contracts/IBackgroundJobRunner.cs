namespace Application.Contracts;

public interface IBackgroundJobRunner
{
    Task Run(
        long id, 
        CancellationToken cancellationToken = default);
}
