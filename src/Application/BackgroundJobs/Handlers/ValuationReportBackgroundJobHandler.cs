using Application.Attributes;
using Application.Contracts.BackgroundJobs;
using Domain.BackgroundJobs;

namespace Application.BackgroundJobs.Handlers;

[BackgroundJobHandler(BackgroundJobType.ValuationReport)]
class ValuationReportBackgroundJobHandler : BackgroundJobHandler<ValuationReportBackgroundJobHandlerDto>
{
    protected override Task Handle(
        Guid id,
        ValuationReportBackgroundJobHandlerDto valuationReportBackgroundJobDto,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
