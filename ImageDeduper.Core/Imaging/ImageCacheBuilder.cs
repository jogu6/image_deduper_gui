using System.Diagnostics;
using ImageDeduper.Core.Engine;
using ImageDeduper.Core.Logging;
using ImageDeduper.Core.Models;
using ImageDeduper.Core.Persistence;
using ImageDeduper.Core.Localization;
using ImageDeduper.Core.Utils;

namespace ImageDeduper.Core.Imaging;

public sealed class ImageCacheBuilder
{
    private readonly ImagePreprocessor _preprocessor;
    private readonly DeduperLogger _logger;
    private readonly Engine.ProgressReporter _progress;
    private readonly ImageCacheStore _cacheStore;
    private readonly Translator _translator;
    private readonly ProblemFileHandler _problemFileHandler;

    public ImageCacheBuilder(ImagePreprocessor preprocessor, DeduperLogger logger, Engine.ProgressReporter progress, ImageCacheStore cacheStore, Translator translator, ProblemFileHandler problemFileHandler)
    {
        _preprocessor = preprocessor;
        _logger = logger;
        _progress = progress;
        _cacheStore = cacheStore;
        _translator = translator;
        _problemFileHandler = problemFileHandler;
    }

    public async Task<IReadOnlyList<ImageRecord>> BuildAsync(IReadOnlyList<string> paths, string folderPath, string duplicatesDir, int startIndex, CancellationToken cancellationToken)
    {
        var records = new List<ImageRecord>(paths.Count);
        _progress.StartPhase(Models.ProgressPhase.Loading, Math.Max(1, paths.Count), _translator.T("status.estimating"));
        await _logger.LogAsync(_translator.T("log.loading_cache", ("count", paths.Count)), cancellationToken: cancellationToken).ConfigureAwait(false);

        var watch = Stopwatch.StartNew();
        var processed = 0;
        var snapshot = await _cacheStore.LoadAsync(folderPath, cancellationToken).ConfigureAwait(false);
        var reused = 0;
        var resumeIndex = Math.Clamp(startIndex, 0, Math.Max(0, paths.Count));

        if (resumeIndex > 0)
        {
            for (var i = 0; i < resumeIndex; i++)
            {
                if (!snapshot.TryGet(paths[i], out var cached) || !File.Exists(paths[i]))
                {
                    resumeIndex = i;
                    break;
                }

                records.Add(cached);
                reused++;
            }

            processed = resumeIndex;
            _progress.ReportProgress(processed);
        }

        string? lastProcessedPath = resumeIndex > 0 && resumeIndex - 1 < paths.Count ? paths[resumeIndex - 1] : null;

        try
        {
            for (var index = resumeIndex; index < paths.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                processed++;
                var path = paths[index];
                lastProcessedPath = path;

                if (snapshot.TryGet(path, out var cached) && File.Exists(path))
                {
                    records.Add(cached);
                    reused++;
                    _progress.ReportProgress(processed);
                    continue;
                }

                var record = await _preprocessor.BuildRecordAsync(path, cancellationToken).ConfigureAwait(false);
                if (record is not null)
                {
                    records.Add(record);
                    await _cacheStore.AppendAsync(folderPath, record, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await _problemFileHandler.MoveCorruptFileAsync(path, duplicatesDir, cancellationToken).ConfigureAwait(false);
                }

                _progress.ReportProgress(processed);
                if (processed % 10 == 0)
                {
                    var eta = EstimateEta(watch.Elapsed, processed, paths.Count);
                    _progress.UpdateEta(eta);
                }
            }
        }
        catch (OperationCanceledException exc)
        {
            throw new LoadingCanceledException(processed, lastProcessedPath, exc);
        }

        _progress.UpdateEta(_translator.T("status.done"));
        await _logger.LogAsync(_translator.T("log.loading_done", ("count", records.Count), ("reused", reused)), cancellationToken: cancellationToken).ConfigureAwait(false);
        return records;
    }

    private string EstimateEta(TimeSpan elapsed, int done, int total)
    {
        if (done == 0)
        {
            return _translator.T("status.estimating");
        }

        var perItem = elapsed.TotalSeconds / done;
        var remaining = Math.Max(0, total - done);
        var etaTime = DateTime.Now + TimeSpan.FromSeconds(remaining * perItem);
        return etaTime.ToString("yyyy-MM-dd HH:mm:ss");
    }

}
