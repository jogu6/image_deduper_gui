using ImageDeduper.Core.Configuration;
using ImageDeduper.Core.Engine;
using ImageDeduper.Core.Logging;
using ImageDeduper.Core.Models;

namespace ImageDeduper.App.Services;

public sealed class DeduperController : IDisposable
{
    private readonly AppSettings _settings;
    private CancellationTokenSource? _cts;
    private DuplicateDetector? _detector;

    public DeduperController(AppSettings? settings = null)
    {
        _settings = settings ?? AppSettings.LoadOrCreate();
    }

    public AppSettings Settings => _settings;

    public bool IsRunning => _cts is not null;

    public event EventHandler<ProgressUpdate>? ProgressChanged;
    public event EventHandler<LogEntry>? LogReceived;
    public event EventHandler<StatsSnapshot>? StatsUpdated;

    public async Task StartAsync(string folderPath)
    {
        if (IsRunning)
        {
            throw new InvalidOperationException("Duplicate detector is already running.");
        }

        _cts = new CancellationTokenSource();
        _detector = new DuplicateDetector(_settings);
        _detector.ProgressChanged += HandleProgressChanged;
        _detector.LogReceived += HandleLogReceived;
        _detector.StatsUpdated += HandleStatsUpdated;

        try
        {
            await Task.Run(() => _detector!.RunAsync(folderPath, _cts.Token)).ConfigureAwait(false);
        }
        finally
        {
            _detector.ProgressChanged -= HandleProgressChanged;
            _detector.LogReceived -= HandleLogReceived;
            _detector.StatsUpdated -= HandleStatsUpdated;
            _detector.Dispose();
            _detector = null;

            _cts.Dispose();
            _cts = null;
        }
    }

    public void Cancel()
    {
        _cts?.Cancel();
    }

    public void UpdateSettings(double ssimThreshold, int phashThreshold, string language)
    {
        _settings.DefaultSsimThreshold = ssimThreshold;
        _settings.PHashThreshold = phashThreshold;
        _settings.Language = language;
        _settings.Save();
    }

    private void HandleProgressChanged(object? sender, ProgressUpdate e) => ProgressChanged?.Invoke(this, e);
    private void HandleLogReceived(object? sender, LogEntry e) => LogReceived?.Invoke(this, e);
    private void HandleStatsUpdated(object? sender, StatsSnapshot e) => StatsUpdated?.Invoke(this, e);

    public void Dispose()
    {
        _cts?.Cancel();
        _detector?.Dispose();
        _cts?.Dispose();
    }
}
