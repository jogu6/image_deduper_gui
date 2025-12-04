using System.Linq;
using System.Windows.Media.Imaging;
using ImageDeduper.Core.Logging;
using ImageDeduper.Core.Models;
using ImageDeduper.Core.Utils;
using ImageDeduper.Core.Localization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ImageDeduper.Core.Imaging;

public sealed class ImagePreprocessor
{
    private static readonly string[] SupportedExtensions = [".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff", ".webp", ".jfif", ".heic", ".heif"];

    private readonly DeduperLogger _logger;
    private readonly Translator _translator;

    public ImagePreprocessor(DeduperLogger logger, Translator translator)
    {
        _logger = logger;
        _translator = translator;
    }

    public bool IsSupported(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return SupportedExtensions.Contains(ext);
    }

    public async Task<string?> NormalizeAsync(string path, string duplicatesDir, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var ext = Path.GetExtension(path).ToLowerInvariant();

        if (ext is ".heic" or ".heif")
        {
            var converted = await ConvertHeicToJpegAsync(path, duplicatesDir, cancellationToken).ConfigureAwait(false);
            if (converted is null)
            {
                return null;
            }

            path = converted;
        }

        if (Path.GetExtension(path).Equals(".jfif", StringComparison.OrdinalIgnoreCase))
        {
            path = await RenameExtensionAsync(path, ".jpg", cancellationToken).ConfigureAwait(false) ?? path;
        }

        path = await FixExtensionAsync(path, cancellationToken).ConfigureAwait(false) ?? path;
        return path;
    }

    private async Task<string?> ConvertHeicToJpegAsync(string path, string duplicatesDir, CancellationToken cancellationToken)
    {
        var newPath = Path.ChangeExtension(path, ".jpg");

        try
        {
            await Task.Run(() =>
            {
                var bitmap = LoadBitmapFrame(path);
                if (bitmap is null)
                {
                    throw new InvalidOperationException("HEIC codec is not available on this system.");
                }

                if (File.Exists(newPath))
                {
                    File.Delete(newPath);
                }

                var encoder = new JpegBitmapEncoder
                {
                    QualityLevel = 95
                };
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var fs = File.Create(newPath);
                encoder.Save(fs);
            }, cancellationToken).ConfigureAwait(false);

            Directory.CreateDirectory(duplicatesDir);
            var destination = Path.Combine(duplicatesDir, Path.GetFileName(path));
            if (File.Exists(destination))
            {
                File.Delete(destination);
            }

            File.Move(path, destination);
            await _logger.LogAsync(_translator.T("log.convert_heic"), cancellationToken: cancellationToken).ConfigureAwait(false);
            return newPath;
        }
        catch (Exception exc)
        {
            var hint = _translator.T("hint.heic_install");
            await _logger.LogAsync(_translator.T("log.convert_heic_fail", ("path", path), ("hint", hint)), LogLevel.Error, exc, cancellationToken).ConfigureAwait(false);
            return null;
        }
    }

    private static BitmapSource? LoadBitmapFrame(string path)
    {
        try
        {
            using var fs = File.OpenRead(path);
            var decoder = BitmapDecoder.Create(fs, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            return decoder.Frames.FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private async Task<string?> RenameExtensionAsync(string path, string newExtension, CancellationToken cancellationToken)
    {
        try
        {
            var newPath = Path.ChangeExtension(path, newExtension);
            File.Move(path, newPath, overwrite: false);
            await _logger.LogAsync(_translator.T("log.rename_jfif", ("path", path), ("newPath", newPath)), cancellationToken: cancellationToken).ConfigureAwait(false);
            return newPath;
        }
        catch (IOException)
        {
            return null;
        }
        catch (Exception exc)
        {
            await _logger.LogAsync(_translator.T("log.rename_jfif_fail", ("path", path)), LogLevel.Error, exc, cancellationToken).ConfigureAwait(false);
            return null;
        }
    }

    private async Task<string?> FixExtensionAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = File.OpenRead(path);
            var format = await Image.DetectFormatAsync(stream, cancellationToken).ConfigureAwait(false);
            if (format is null)
            {
                return path;
            }

            var detectedExt = format.FileExtensions.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(detectedExt))
            {
                return path;
            }

            var expectedExt = "." + detectedExt;
            if (Path.GetExtension(path).Equals(expectedExt, StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }

            var newPath = Path.ChangeExtension(path, expectedExt);
            if (File.Exists(newPath))
            {
                return path;
            }

            File.Move(path, newPath);
            await _logger.LogAsync(_translator.T("log.extension_fixed", ("path", newPath)), cancellationToken: cancellationToken).ConfigureAwait(false);
            return newPath;
        }
        catch (Exception exc)
        {
            await _logger.LogAsync(_translator.T("log.extension_fail", ("path", path)), LogLevel.Warning, exc, cancellationToken).ConfigureAwait(false);
            return path;
        }
    }

    public async Task<ImageRecord?> BuildRecordAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            using var image = await Image.LoadAsync<L8>(path, cancellationToken).ConfigureAwait(false);
            var width = image.Width;
            var height = image.Height;
            var pixels224 = ExtractPixels(image, 224, cancellationToken);
            var pixels32 = ExtractPixels(image, 32, cancellationToken);
            var pHash = PHashCalculator.Compute(pixels32);
            var sha1 = FileHasher.ComputeSha1(path);
            var fileInfo = new FileInfo(path);
            return new ImageRecord(path, width, height, fileInfo.Length, pHash, sha1, pixels224);
        }
        catch (Exception exc)
        {
            await _logger.LogAsync(_translator.T("log.build_cache_fail", ("path", path)), LogLevel.Error, exc, cancellationToken).ConfigureAwait(false);
            return null;
        }
    }

    private static float[] ExtractPixels(Image<L8> source, int size, CancellationToken cancellationToken)
    {
        using var clone = source.Clone(ctx =>
        {
            ctx.Resize(new ResizeOptions
            {
                Size = new Size(size, size),
                Sampler = KnownResamplers.Lanczos3
            });
        });

        var pixels = new float[size * size];
        var index = 0;

        clone.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var span = accessor.GetRowSpan(y);
                for (var x = 0; x < span.Length; x++)
                {
                    pixels[index++] = span[x].PackedValue;
                }
            }
        });

        return pixels;
    }
}
