namespace ImageDeduper.Core.Models;

public sealed record ProgressUpdate(
    int Completed,
    int Total,
    ProgressPhase Phase,
    string EtaText);
