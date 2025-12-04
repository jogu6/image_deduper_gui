using System.Text;

namespace ImageDeduper.Core.Logging;

public sealed class DeduperLogger : IDisposable
{
    private readonly string _logDirectory;
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private DateTime? _lastLogTime;
    private bool _disposed;

    public DeduperLogger(string logDirectory)
    {
        _logDirectory = logDirectory;
        Directory.CreateDirectory(_logDirectory);
    }

    public event EventHandler<LogEntry>? LogReceived;

    public async Task LogAsync(string message, LogLevel level = LogLevel.Info, Exception? exception = null, CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var now = DateTime.Now;
            var delta = _lastLogTime.HasValue ? now - _lastLogTime.Value : TimeSpan.Zero;
            _lastLogTime = now;

            var entry = new LogEntry(now, delta, level, message);
            WriteToLogFile(entry);

            LogReceived?.Invoke(this, entry);

            if (exception is not null)
            {
                WriteTraceLog(now, message, exception);
            }
        }
        finally
        {
            _mutex.Release();
        }
    }

    public void Log(string message, LogLevel level = LogLevel.Info, Exception? exception = null)
    {
        LogAsync(message, level, exception).GetAwaiter().GetResult();
    }

    private void WriteToLogFile(LogEntry entry)
    {
        var logFile = Path.Combine(_logDirectory, $"imagededuper_{entry.Timestamp:yyyyMM}.log");
        var deltaText = entry.Delta == TimeSpan.Zero ? "------" : entry.Delta.ToString(@"hhmmss");
        var prefix = $"[{entry.Timestamp:yyyyMMdd HHmmss} {deltaText}]";
        var line = $"{prefix} [{entry.Level}] {entry.Message}";
        File.AppendAllText(logFile, line + Environment.NewLine, Encoding.UTF8);
    }

    private void WriteTraceLog(DateTime timestamp, string description, Exception exception)
    {
        var name = Path.Combine(_logDirectory, $"error_traceback_{timestamp:yyyyMMdd}.log");
        var sb = new StringBuilder();
        sb.AppendLine(new string('-', 50));
        sb.AppendLine($"[{timestamp:yyyyMMdd HHmmss}] {description}");
        sb.AppendLine(exception.ToString());
        sb.AppendLine(new string('-', 50));
        File.AppendAllText(name, sb.ToString(), Encoding.UTF8);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _mutex.Dispose();
        _disposed = true;
    }
}
