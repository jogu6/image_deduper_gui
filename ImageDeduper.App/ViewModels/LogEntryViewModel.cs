using ImageDeduper.Core.Logging;

namespace ImageDeduper.App.ViewModels;

public sealed class LogEntryViewModel
{
    public LogEntryViewModel(LogEntry entry)
    {
        Timestamp = entry.Timestamp;
        Delta = entry.Delta;
        Level = entry.Level;
        Message = entry.Message;
    }

    public DateTime Timestamp { get; }
    public TimeSpan Delta { get; }
    public LogLevel Level { get; }
    public string Message { get; }

    public string Display => $"[{Timestamp:HH:mm:ss}] {Message}";
}
