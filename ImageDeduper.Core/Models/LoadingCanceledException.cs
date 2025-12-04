namespace ImageDeduper.Core.Models;

public sealed class LoadingCanceledException : OperationCanceledException
{
    public LoadingCanceledException(int completed, string? lastPath, Exception? inner = null)
        : base("Image cache loading was canceled.", inner)
    {
        Completed = completed;
        LastPath = lastPath;
    }

    public int Completed { get; }
    public string? LastPath { get; }
}
