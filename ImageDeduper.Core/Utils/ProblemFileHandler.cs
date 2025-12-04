using ImageDeduper.Core.Localization;
using ImageDeduper.Core.Logging;

namespace ImageDeduper.Core.Utils;

public sealed class ProblemFileHandler
{
    private readonly DeduperLogger _logger;
    private readonly Translator _translator;

    public ProblemFileHandler(DeduperLogger logger, Translator translator)
    {
        _logger = logger;
        _translator = translator;
    }

    public async Task MoveCorruptFileAsync(string path, string duplicatesDir, CancellationToken cancellationToken)
    {
        try
        {
            Directory.CreateDirectory(duplicatesDir);
            var destination = Path.Combine(duplicatesDir, Path.GetFileName(path));
            if (File.Exists(destination))
            {
                File.Delete(destination);
            }

            File.Move(path, destination);
            await _logger.LogAsync(_translator.T("log.moved_corrupt", ("path", path)), LogLevel.Warning, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exc)
        {
            await _logger.LogAsync(_translator.T("log.move_failed", ("source", path)), LogLevel.Error, exc, cancellationToken).ConfigureAwait(false);
        }
    }
}
