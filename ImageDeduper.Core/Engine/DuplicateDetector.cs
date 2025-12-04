using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using ImageDeduper.Core.Configuration;
using ImageDeduper.Core.Localization;
using ImageDeduper.Core.Logging;
using ImageDeduper.Core.Models;
using ImageDeduper.Core.Persistence;
using ImageDeduper.Core.Imaging;
using ImageDeduper.Core.Utils;

namespace ImageDeduper.Core.Engine;

public sealed class DuplicateDetector : IDisposable
{
    private readonly AppSettings _settings;
    private readonly Translator _translator;
    private readonly DeduperLogger _logger;
    private readonly ResumeStore _resumeStore;
    private readonly ProgressReporter _progressReporter;
    private readonly ImagePreprocessor _preprocessor;
    private readonly ImageCacheBuilder _cacheBuilder;
    private readonly ImageCacheStore _cacheStore;
    private readonly LoadingCheckpointStore _loadingCheckpointStore;
    private readonly SafeFileMover _fileMover;
    private readonly ProblemFileHandler _problemFileHandler;

    private readonly ConcurrentDictionary<(ulong, ulong), int> _hammingCache = new();
    private readonly Stopwatch _moveStopwatch = new();
    private bool _disposed;

    public DuplicateDetector(AppSettings settings)
    {
        _settings = settings;
        _translator = new Translator(settings.Language);
        _logger = new DeduperLogger(settings.LogDirectory);
        _resumeStore = new ResumeStore(settings.ResumeFileName);
        _progressReporter = new ProgressReporter();
        _preprocessor = new ImagePreprocessor(_logger, _translator);
        _fileMover = new SafeFileMover(_logger, _translator);
        _problemFileHandler = new ProblemFileHandler(_logger, _translator);
        _cacheStore = new ImageCacheStore(_logger, settings.CacheFileName, _translator);
        _loadingCheckpointStore = new LoadingCheckpointStore(settings.LoadingCheckpointFileName);
        _cacheBuilder = new ImageCacheBuilder(_preprocessor, _logger, _progressReporter, _cacheStore, _translator, _problemFileHandler);
    }

    public event EventHandler<ProgressUpdate>? ProgressChanged
    {
        add => _progressReporter.ProgressChanged += value;
        remove => _progressReporter.ProgressChanged -= value;
    }

    public event EventHandler<LogEntry>? LogReceived
    {
        add => _logger.LogReceived += value;
        remove => _logger.LogReceived -= value;
    }

    public event EventHandler<StatsSnapshot>? StatsUpdated;

    public async Task RunAsync(string folderPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            throw new ArgumentException("Folder path is required.", nameof(folderPath));
        }

        if (!Directory.Exists(folderPath))
        {
            throw new DirectoryNotFoundException($"Folder not found: {folderPath}");
        }

        _moveStopwatch.Restart();
        await _logger.LogAsync(_translator.T("log.scanning_folder", ("folder", folderPath)), cancellationToken: cancellationToken).ConfigureAwait(false);

        var allFiles = EnumerateImages(folderPath).ToList();
        if (allFiles.Count == 0)
        {
            await _logger.LogAsync(_translator.T("log.no_images"), cancellationToken: cancellationToken).ConfigureAwait(false);
            await _cacheStore.DeleteAsync(folderPath).ConfigureAwait(false);
            return;
        }

        await _logger.LogAsync(_translator.T("log.collected_candidates", ("count", allFiles.Count)), cancellationToken: cancellationToken).ConfigureAwait(false);

        var duplicatesDir = Path.Combine(folderPath, "duplicates");
        Directory.CreateDirectory(duplicatesDir);

        var normalized = new List<string>(allFiles.Count);
        foreach (var file in allFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var finalPath = await _preprocessor.NormalizeAsync(file, duplicatesDir, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(finalPath))
            {
                await _problemFileHandler.MoveCorruptFileAsync(file, duplicatesDir, cancellationToken).ConfigureAwait(false);
                continue;
            }

            normalized.Add(finalPath);
        }

        var loadingCheckpoint = await _loadingCheckpointStore.LoadAsync(folderPath, cancellationToken).ConfigureAwait(false);
        var loadingStartIndex = ResolveLoadingStartIndex(normalized, loadingCheckpoint);
        if (loadingStartIndex > 0)
        {
            var resumeName = Path.GetFileName(loadingCheckpoint?.Path ?? string.Empty);
            await _logger.LogAsync(_translator.T("log.resuming_loading", ("index", loadingStartIndex), ("total", normalized.Count), ("name", resumeName ?? string.Empty)), cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        IReadOnlyList<ImageRecord> cachedImages;
        try
        {
            cachedImages = await _cacheBuilder.BuildAsync(normalized, folderPath, duplicatesDir, loadingStartIndex, cancellationToken).ConfigureAwait(false);
            await _loadingCheckpointStore.DeleteAsync(folderPath).ConfigureAwait(false);
        }
        catch (LoadingCanceledException lce)
        {
            var checkpoint = new LoadingCheckpoint
            {
                Index = Math.Clamp(lce.Completed, 0, normalized.Count),
                Total = normalized.Count,
                Path = lce.LastPath
            };
            await _loadingCheckpointStore.SaveAsync(folderPath, checkpoint, CancellationToken.None).ConfigureAwait(false);
            throw;
        }

        var sorted = cachedImages.OrderBy(record => record.PerceptualHash).ToList();
        var n = sorted.Count;

        if (n < 2)
        {
            await _logger.LogAsync(_translator.T("log.only_one_image"), cancellationToken: cancellationToken).ConfigureAwait(false);
            await _cacheStore.DeleteAsync(folderPath).ConfigureAwait(false);
            return;
        }

        var totalPairs = n * (n - 1) / 2;
        var resume = await _resumeStore.LoadAsync(folderPath, cancellationToken).ConfigureAwait(false);
        var currentProgress = resume?.CurrentProgress ?? 0;
        var moved = resume?.Moved ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var movedBefore = new HashSet<string>(moved, StringComparer.OrdinalIgnoreCase);
        var startI = resume?.I ?? 0;
        var startJ = resume?.J ?? 1;

        if (resume is not null)
        {
            await _logger.LogAsync(_translator.T("log.resuming_state", ("i", startI), ("j", startJ)), cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await _logger.LogAsync(_translator.T("log.starting_detection"), cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        _progressReporter.StartPhase(ProgressPhase.Comparing, totalPairs, _translator.T("status.estimating"));
        _progressReporter.ReportProgress(currentProgress);

        var moveCount = 0;
        var lastI = startI;
        var lastJ = startJ;
        var compareWatch = Stopwatch.StartNew();

        try
        {
            for (var i = startI; i < n - 1; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                lastI = i;

                await OnNewBaseAsync(sorted, i, n, folderPath, moved, currentProgress, cancellationToken).ConfigureAwait(false);

                var jStart = i == startI ? startJ : i + 1;
                for (var j = jStart; j < n; j++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    lastJ = j;

                    var res = ComparePair(sorted[i], sorted[j]);
                    currentProgress++;
                    _progressReporter.ReportProgress(currentProgress);

                    if (currentProgress % 10 == 0)
                    {
                        await SaveResumeAsync(folderPath, i, j, moved, currentProgress, cancellationToken).ConfigureAwait(false);
                        UpdateEta(compareWatch.Elapsed, currentProgress, totalPairs);
                    }

                    if (res is null)
                    {
                        continue;
                    }

                    if (moved.Contains(res.PathA) || moved.Contains(res.PathB))
                    {
                        continue;
                    }

                    var toMove = ChooseSmaller(sorted[i], sorted[j]);
                    var destination = Path.Combine(duplicatesDir, Path.GetFileName(toMove.Path));
                    var movedOk = await _fileMover.MoveAsync(toMove.Path, destination, cancellationToken).ConfigureAwait(false);
                    if (movedOk)
                    {
                        moved.Add(toMove.Path);
                        moveCount++;
                        var msg = res.ShaMatch
                            ? _translator.T("log.moved_sha", ("path", toMove.Path))
                            : _translator.T("log.moved_ssim", ("score", res.Score.ToString("F4")), ("path", toMove.Path));
                        await _logger.LogAsync(msg, cancellationToken: cancellationToken).ConfigureAwait(false);
                        PublishStats(n, i + 1, moved.Count, _moveStopwatch.Elapsed);
                    }
                }
            }

        await _resumeStore.DeleteAsync(folderPath).ConfigureAwait(false);
        await _loadingCheckpointStore.DeleteAsync(folderPath).ConfigureAwait(false);
        await _cacheStore.DeleteAsync(folderPath).ConfigureAwait(false);
            await _logger.LogAsync(_translator.T("log.duplicates_complete"), cancellationToken: cancellationToken).ConfigureAwait(false);
            await _logger.LogAsync(_translator.T("log.moved_new_files", ("count", moved.Count - movedBefore.Count)), cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await SaveResumeAsync(folderPath, lastI, lastJ, moved, currentProgress, CancellationToken.None).ConfigureAwait(false);
            await _loadingCheckpointStore.DeleteAsync(folderPath).ConfigureAwait(false);
            await _logger.LogAsync(_translator.T("log.detection_interrupted"), LogLevel.Warning).ConfigureAwait(false);
            throw;
        }
    }

    private async Task OnNewBaseAsync(
        IReadOnlyList<ImageRecord> sorted,
        int index,
        int total,
        string folderPath,
        HashSet<string> moved,
        int currentProgress,
        CancellationToken cancellationToken)
    {
        await SaveResumeAsync(folderPath, index, index + 1, moved, currentProgress, cancellationToken).ConfigureAwait(false);
        await _logger.LogAsync(_translator.T("log.base_status", ("index", index + 1), ("total", total), ("name", Path.GetFileName(sorted[index].Path) ?? sorted[index].Path)), cancellationToken: cancellationToken).ConfigureAwait(false);
        PublishStats(total, index, moved.Count, _moveStopwatch.Elapsed);
    }

    private void PublishStats(int total, int processedBase, int moved, TimeSpan elapsed)
    {
        StatsUpdated?.Invoke(this, new StatsSnapshot(total, processedBase, moved, elapsed));
    }

    private void UpdateEta(TimeSpan elapsed, int progress, int totalPairs)
    {
        if (progress <= 0)
        {
            _progressReporter.UpdateEta(_translator.T("status.estimating"));
            return;
        }

        var avg = elapsed.TotalSeconds / progress;
        var remaining = totalPairs - progress;
        if (remaining <= 0)
        {
            _progressReporter.UpdateEta(_translator.T("status.done"));
            return;
        }

        var eta = DateTime.Now + TimeSpan.FromSeconds(avg * remaining);
        _progressReporter.UpdateEta(eta.ToString("yyyy-MM-dd HH:mm:ss"));
    }

    private ComparisonResult? ComparePair(ImageRecord a, ImageRecord b)
    {
        if (!string.IsNullOrWhiteSpace(a.Sha1) && a.Sha1.Equals(b.Sha1, StringComparison.OrdinalIgnoreCase))
        {
            return new ComparisonResult(0, 0, a.Path, b.Path, 1.0, true);
        }

        var distance = _hammingCache.GetOrAdd((a.PerceptualHash, b.PerceptualHash), static tuple =>
        {
            return PHashCalculator.HammingDistance(tuple.Item1, tuple.Item2);
        });

        if (distance > _settings.PHashThreshold)
        {
            return null;
        }

        var score = SsimCalculator.Compute(a.Pixels, b.Pixels);
        if (_settings.DebugLogSsim)
        {
            _logger.Log(_translator.T("log.debug_ssim", ("score", score.ToString("F4")), ("pathA", a.Path), ("pathB", b.Path)), LogLevel.Debug);
        }

        return score >= _settings.DefaultSsimThreshold
            ? new ComparisonResult(0, 0, a.Path, b.Path, score, false)
            : null;
    }

    private static ImageRecord ChooseSmaller(ImageRecord a, ImageRecord b)
    {
        var resA = a.Resolution;
        var resB = b.Resolution;
        return resA <= resB ? a : b;
    }

    private async Task SaveResumeAsync(string folderPath, int i, int j, HashSet<string> moved, int progress, CancellationToken cancellationToken)
    {
        var state = new ResumeState
        {
            I = i,
            J = j,
            CurrentProgress = progress,
            Moved = new HashSet<string>(moved, StringComparer.OrdinalIgnoreCase)
        };

        await _resumeStore.SaveAsync(folderPath, state, cancellationToken).ConfigureAwait(false);
    }

    private IEnumerable<string> EnumerateImages(string root)
    {
        var stack = new Stack<string>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            IEnumerable<string> dirs;
            IEnumerable<string> files;

            try
            {
                dirs = Directory.EnumerateDirectories(current);
                files = Directory.EnumerateFiles(current);
            }
            catch
            {
                continue;
            }

            foreach (var dir in dirs)
            {
                if (Path.GetFileName(dir).Equals("duplicates", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                stack.Push(dir);
            }

            foreach (var file in files)
            {
                if (_preprocessor.IsSupported(file))
                {
                    yield return file;
                }
            }
        }
    }

    private static int ResolveLoadingStartIndex(IReadOnlyList<string> normalized, LoadingCheckpoint? checkpoint)
    {
        if (checkpoint is null || checkpoint.Index <= 0 || normalized.Count == 0)
        {
            return 0;
        }

        var index = checkpoint.Index;
        if (!string.IsNullOrWhiteSpace(checkpoint.Path))
        {
            for (var i = 0; i < normalized.Count; i++)
            {
                if (string.Equals(normalized[i], checkpoint.Path, StringComparison.OrdinalIgnoreCase))
                {
                    index = i + 1;
                    break;
                }
            }
        }

        return Math.Clamp(index, 0, normalized.Count);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _logger.Dispose();
        _disposed = true;
    }
}
