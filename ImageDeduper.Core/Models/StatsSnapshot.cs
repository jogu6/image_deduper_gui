namespace ImageDeduper.Core.Models;

public sealed record StatsSnapshot(
    int TotalSources,
    int ProcessedBase,
    int MovedDuplicates,
    TimeSpan Elapsed);
