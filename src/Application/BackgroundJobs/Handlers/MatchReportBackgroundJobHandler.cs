using Application.Attributes;
using Application.Contracts.BackgroundJobs;
using Domain.BackgroundJobs;

namespace Application.BackgroundJobs.Handlers;

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
