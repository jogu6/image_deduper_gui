namespace ImageDeduper.Core.Models;

public sealed record ComparisonResult(
    int IndexA,
    int IndexB,
    string PathA,
    string PathB,
    double Score,
    bool ShaMatch);
