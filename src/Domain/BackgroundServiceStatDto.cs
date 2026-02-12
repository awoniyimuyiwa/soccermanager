namespace Domain;

public record BackgroundServiceStatDto(
    Guid Id,
    string? Details,
    DateTimeOffset LastRunAt,
    long Total,
    long TotalInLastRun,
    BackgroundServiceStatType Type);
