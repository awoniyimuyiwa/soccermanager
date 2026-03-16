using Application.Attributes;
using Application.Contracts;
using Domain;

namespace Application.BackgroundJobHandlers;

[BackgroundJobHandler(BackgroundJobType.MatchReport)]
class MatchReportBackgroundJobHandler : BackgroundJobHandler<MatchReportBackgroundJobHandlerDto>
{
    protected override Task Handle(
        Guid id,
        MatchReportBackgroundJobHandlerDto data,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
