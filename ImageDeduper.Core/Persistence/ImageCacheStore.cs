using System.Buffers.Binary;
using System.Text;
using ImageDeduper.Core.Logging;
using ImageDeduper.Core.Models;
using ImageDeduper.Core.Localization;

namespace ImageDeduper.Core.Persistence;

public sealed class ImageCacheStore
{
    private const uint Magic = 0x49434143; // ICAC
    private const int Version = 1;

    private readonly string _cacheFileName;
    private readonly DeduperLogger _logger;
    private readonly Translator _translator;

    public ImageCacheStore(DeduperLogger logger, string cacheFileName, Translator translator)
    {
        _logger = logger;
        _cacheFileName = cacheFileName;
        _translator = translator;
    }

    public async Task<ImageCacheSnapshot> LoadAsync(string folderPath, CancellationToken cancellationToken)
    {
        var path = GetCachePath(folderPath);
        if (!File.Exists(path))
        {
            return new ImageCacheSnapshot();
        }

        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
            var records = new Dictionary<string, ImageRecord>(StringComparer.OrdinalIgnoreCase);
            if (stream.Length < 8)
            {
                return new ImageCacheSnapshot(records);
            }

            var header = new byte[8];
            var read = await stream.ReadAsync(header, cancellationToken).ConfigureAwait(false);
            if (read < 8 || BinaryPrimitives.ReadUInt32LittleEndian(header) != Magic || BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4)) != Version)
            {
                await _logger.LogAsync(_translator.T("log.image_cache_signature_mismatch"), LogLevel.Warning, cancellationToken: cancellationToken).ConfigureAwait(false);
                await DeleteAsync(folderPath).ConfigureAwait(false);
                return new ImageCacheSnapshot(records);
            }

            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            while (stream.Position < stream.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var record = ReadRecord(reader);
                    records[record.Path] = record;
                }
                catch (EndOfStreamException)
                {
                    break;
                }
            }

            return new ImageCacheSnapshot(records);
        }
        catch (Exception ex)
        {
            await _logger.LogAsync(_translator.T("log.image_cache_load_failed", ("path", path)), LogLevel.Warning, ex, cancellationToken).ConfigureAwait(false);
            await DeleteAsync(folderPath).ConfigureAwait(false);
            return new ImageCacheSnapshot();
        }
    }

    public async Task AppendAsync(string folderPath, ImageRecord record, CancellationToken cancellationToken)
    {
        var path = GetCachePath(folderPath);
        Directory.CreateDirectory(folderPath);
        await using var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read, 4096, FileOptions.SequentialScan);
        if (stream.Length == 0)
        {
            await WriteHeaderAsync(stream, cancellationToken).ConfigureAwait(false);
        }

        stream.Seek(0, SeekOrigin.End);
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        WriteRecord(writer, record);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task DeleteAsync(string folderPath)
    {
        var path = GetCachePath(folderPath);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string GetCachePath(string folderPath) => Path.Combine(folderPath, _cacheFileName);

    private static ImageRecord ReadRecord(BinaryReader reader)
    {
        var path = reader.ReadString();
        var width = reader.ReadInt32();
        var height = reader.ReadInt32();
        var length = reader.ReadInt64();
        var perceptualHash = reader.ReadUInt64();
        var sha1 = reader.ReadString();
        var pixelCount = reader.ReadInt32();
        var pixels = new float[pixelCount];
        for (var i = 0; i < pixelCount; i++)
        {
            pixels[i] = reader.ReadByte();
        }

        return new ImageRecord(path, width, height, length, perceptualHash, sha1, pixels);
    }

    private static void WriteRecord(BinaryWriter writer, ImageRecord record)
    {
        writer.Write(record.Path);
        writer.Write(record.Width);
        writer.Write(record.Height);
        writer.Write(record.Length);
        writer.Write(record.PerceptualHash);
        writer.Write(record.Sha1);
        var pixels = record.Pixels ?? Array.Empty<float>();
        writer.Write(pixels.Length);
        for (var i = 0; i < pixels.Length; i++)
        {
            var value = (int)Math.Round(pixels[i]);
            value = Math.Clamp(value, 0, 255);
            writer.Write((byte)value);
        }
    }

    private static async Task WriteHeaderAsync(Stream stream, CancellationToken cancellationToken)
    {
        var header = new byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(header, Magic);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(4), Version);
        await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
    }
}
