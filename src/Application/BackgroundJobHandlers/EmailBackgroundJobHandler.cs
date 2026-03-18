using Application.Attributes;
using Application.Contracts;
using Domain;

namespace Application.BackgroundJobHandlers;

[BackgroundJobHandler(BackgroundJobType.Email)]
class EmailBackgroundJobHandler : BackgroundJobHandler<EmailBackgroundJobHandlerDto>
{
    protected override Task Handle(
        Guid id,
        EmailBackgroundJobHandlerDto data, 
        CancellationToken cancellationToken = default)
    {
        var metadata = new { IdempotencyKey = id.ToString() };

        return Task.CompletedTask;
    }
}
