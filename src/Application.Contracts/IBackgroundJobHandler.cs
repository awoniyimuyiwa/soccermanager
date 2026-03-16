namespace Application.Contracts;

public interface IBackgroundJobHandler
{
    Task Handle(
        Guid id,
        string payload,
        CancellationToken cancellationToken = default);
}
