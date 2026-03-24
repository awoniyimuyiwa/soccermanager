using Domain;

namespace Api.Models.V1;

public record BackgroundServiceStatModel(
    Guid Id,
    object? Details,
    DateTimeOffset LastRunAt,
    long Total,
    long TotalInLastRun,
    BackgroundServiceStatType Type);
