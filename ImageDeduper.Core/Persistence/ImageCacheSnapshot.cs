using ImageDeduper.Core.Models;

namespace ImageDeduper.Core.Persistence;

public sealed class ImageCacheSnapshot
{
    private readonly Dictionary<string, ImageRecord> _records;

    public ImageCacheSnapshot()
    {
        _records = new Dictionary<string, ImageRecord>(StringComparer.OrdinalIgnoreCase);
    }

    public ImageCacheSnapshot(Dictionary<string, ImageRecord> records)
    {
        _records = records;
    }

    public int Count => _records.Count;

    public bool TryGet(string path, out ImageRecord record) => _records.TryGetValue(path, out record!);

    public IReadOnlyCollection<ImageRecord> AllRecords => _records.Values;
}
