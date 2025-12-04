using ImageDeduper.Core.Models;

namespace ImageDeduper.Core.Engine;

public sealed class ProgressReporter
{
    private ProgressPhase _phase = ProgressPhase.None;
    private int _completed;
    private int _total = 1;
    private string _etaText = "Estimating...";

    public event EventHandler<ProgressUpdate>? ProgressChanged;

    public void StartPhase(ProgressPhase phase, int total, string etaText = "Estimating...")
    {
        _phase = phase;
        _total = Math.Max(1, total);
        _completed = 0;
        _etaText = etaText;
        Raise();
    }

    public void UpdateEta(string etaText)
    {
        _etaText = etaText;
        Raise();
    }

    public void ReportProgress(int completed)
    {
        _completed = completed;
        Raise();
    }

    private void Raise()
    {
        ProgressChanged?.Invoke(this, new ProgressUpdate(_completed, _total, _phase, _etaText));
    }
}
