using Application.Attributes;
using Application.Contracts;
using Domain;

namespace Application.BackgroundJobHandlers;

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
