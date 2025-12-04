using System.Text.Json;

namespace ImageDeduper.Core.Persistence;

public sealed class ResumeStore
{
    private readonly string _fileName;

    public ResumeStore(string fileName)
    {
        _fileName = fileName;
    }

    public async Task SaveAsync(string folderPath, ResumeState state, CancellationToken cancellationToken = default)
    {
        var path = GetResumePath(folderPath);
        var json = JsonSerializer.Serialize(state, ResumeState.JsonOptions);
        await File.WriteAllTextAsync(path, json, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ResumeState?> LoadAsync(string folderPath, CancellationToken cancellationToken = default)
    {
        var path = GetResumePath(folderPath);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<ResumeState>(stream, ResumeState.JsonOptions, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    public Task DeleteAsync(string folderPath)
    {
        var path = GetResumePath(folderPath);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string GetResumePath(string folderPath) => Path.Combine(folderPath, _fileName);
}

public sealed class ResumeState
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public int I { get; set; }
    public int J { get; set; }
    public HashSet<string> Moved { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public int CurrentProgress { get; set; }
}
