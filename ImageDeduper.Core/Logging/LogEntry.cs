namespace ImageDeduper.Core.Logging;

public sealed record LogEntry(
    DateTime Timestamp,
    TimeSpan Delta,
    LogLevel Level,
    string Message);

public enum LogLevel
{
    Info,
    Warning,
    Error,
    Debug
}
