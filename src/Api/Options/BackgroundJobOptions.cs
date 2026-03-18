using System.ComponentModel.DataAnnotations;

namespace Api.Options;

public class BackgroundJobOptions
{
    public const string SectionName = "BackgroundJobOptions";

    [Range(1, 100)]
    public int BatchSize { get; set; } = 100;

    [Range(1, 10)]
    public int MaxRetries { get; set; } = 3;

    [Range(1, 60)]
    public int PollingIntervalSeconds { get; set; } = 60;

    [Range(1, 1440)] // Up to 24 hours
    public int StuckJobThresholdMinutes { get; set; } = 30;
}

