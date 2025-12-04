using System.Text.Json;

namespace ImageDeduper.Core.Persistence;

public sealed class LoadingCheckpointStore
{
    private readonly string _fileName;

    public LoadingCheckpointStore(string fileName)
    {
        _fileName = fileName;
    }

    public async Task SaveAsync(string folderPath, LoadingCheckpoint checkpoint, CancellationToken cancellationToken = default)
    {
        var path = GetPath(folderPath);
        var json = JsonSerializer.Serialize(checkpoint, LoadingCheckpoint.JsonOptions);
        await File.WriteAllTextAsync(path, json, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LoadingCheckpoint?> LoadAsync(string folderPath, CancellationToken cancellationToken = default)
    {
        var path = GetPath(folderPath);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<LoadingCheckpoint>(stream, LoadingCheckpoint.JsonOptions, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    public Task DeleteAsync(string folderPath)
    {
        var path = GetPath(folderPath);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string GetPath(string folderPath) => Path.Combine(folderPath, _fileName);
}

public sealed class LoadingCheckpoint
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public int Index { get; set; }
    public int Total { get; set; }
    public string? Path { get; set; }
}
