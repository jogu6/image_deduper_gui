using ImageDeduper.Core.Logging;
using ImageDeduper.Core.Localization;

namespace ImageDeduper.Core.Utils;

public sealed class SafeFileMover
{
    private readonly DeduperLogger _logger;
    private readonly Translator _translator;

    public SafeFileMover(DeduperLogger logger, Translator translator)
    {
        _logger = logger;
        _translator = translator;
    }

    public async Task<bool> MoveAsync(string source, string destination, CancellationToken cancellationToken)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Move(source, destination, overwrite: false);
            return true;
        }
        catch (IOException)
        {
            if (!File.Exists(destination))
            {
                return false;
            }

            await _logger.LogAsync(_translator.T("log.name_conflict", ("name", Path.GetFileName(source) ?? source)), LogLevel.Warning, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            try
            {
                var hashSrc = FileHasher.ComputeSha1(source);
                var hashDst = FileHasher.ComputeSha1(destination);

                if (!hashSrc.Equals(hashDst, StringComparison.OrdinalIgnoreCase))
                {
                    await _logger.LogAsync(_translator.T("log.sha_mismatch"), LogLevel.Warning, cancellationToken: cancellationToken)
                        .ConfigureAwait(false);
                    return false;
                }

                var srcTime = File.GetLastWriteTimeUtc(source);
                var dstTime = File.GetLastWriteTimeUtc(destination);
                var toRemove = srcTime <= dstTime ? source : destination;
                File.Delete(toRemove);

                if (toRemove == destination)
                {
                    File.Move(source, destination, overwrite: false);
                }

                await _logger.LogAsync(_translator.T("log.removed_duplicate", ("path", toRemove)), LogLevel.Info, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }
            catch (Exception exc)
            {
                await _logger.LogAsync(_translator.T("log.resolve_conflict_failed"), LogLevel.Error, exc, cancellationToken).ConfigureAwait(false);
                return false;
            }
        }
        catch (Exception exc)
        {
            await _logger.LogAsync(_translator.T("log.move_failed", ("source", source)), LogLevel.Error, exc, cancellationToken).ConfigureAwait(false);
            return false;
        }
    }
}
