using System.Globalization;

namespace ImageDeduper.Core.Configuration;

public sealed class AppSettings
{
    private const string SettingsFileName = "setting.ini";
    private const int MinWindowWidth = 720;
    private const int MinWindowHeight = 520;

    public string Language { get; set; } = "en";
    public double DefaultSsimThreshold { get; set; } = 0.85;
    public int PHashThreshold { get; set; } = 40;
    public bool DebugLogSsim { get; set; }
    public int ProgressBarWidth { get; set; } = 30;
    public string ResumeFileName { get; set; } = "resume.json";
    public string LoadingCheckpointFileName { get; set; } = ".loading.json";
    public string CacheFileName { get; set; } = ".imagecache.bin";
    public string LogDirectory { get; set; } = Path.Combine(AppContext.BaseDirectory, "log");
    public int WindowWidth { get; set; } = 1024;
    public int WindowHeight { get; set; } = 720;

    public static AppSettings LoadOrCreate(string? path = null)
    {
        var resolvedPath = path ?? GetDefaultSettingsPath();
        if (!File.Exists(resolvedPath))
        {
            var defaults = new AppSettings();
            defaults.Save(resolvedPath);
            return defaults;
        }

        try
        {
            return LoadFromIni(resolvedPath);
        }
        catch
        {
            var fallback = new AppSettings();
            fallback.Save(resolvedPath);
            return fallback;
        }
    }

    public void Save(string? path = null)
    {
        var resolvedPath = path ?? GetDefaultSettingsPath();
        var lines = new[]
        {
            "[ImageDeduper]",
            $"Language={Language}",
            $"DefaultSsimThreshold={DefaultSsimThreshold.ToString(CultureInfo.InvariantCulture)}",
            $"PHashThreshold={PHashThreshold}",
            $"DebugLogSsim={DebugLogSsim}",
            $"ResumeFileName={ResumeFileName}",
            $"LoadingCheckpointFileName={LoadingCheckpointFileName}",
            $"CacheFileName={CacheFileName}",
            $"LogDirectory={LogDirectory}",
            $"WindowWidth={WindowWidth}",
            $"WindowHeight={WindowHeight}"
        };

        File.WriteAllLines(resolvedPath, lines);
        EnsureFolders();
    }

    private static AppSettings LoadFromIni(string path)
    {
        var settings = new AppSettings();
        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#", StringComparison.OrdinalIgnoreCase) || line.StartsWith(";", StringComparison.OrdinalIgnoreCase) || line.StartsWith("[", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var separatorIndex = line.IndexOf('=', StringComparison.Ordinal);
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();

            switch (key.ToLowerInvariant())
            {
                case "language":
                    settings.Language = value;
                    break;
                case "defaultssimthreshold":
                    if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var ssim))
                    {
                        settings.DefaultSsimThreshold = Math.Clamp(ssim, 0.5, 1.0);
                    }
                    break;
                case "phashthreshold":
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var phash))
                    {
                        settings.PHashThreshold = Math.Clamp(phash, 0, 64);
                    }
                    break;
                case "debuglogssim":
                    if (bool.TryParse(value, out var debug))
                    {
                        settings.DebugLogSsim = debug;
                    }
                    break;
                case "resumefilename":
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        settings.ResumeFileName = value;
                    }
                    break;
                case "loadingcheckpointfilename":
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        settings.LoadingCheckpointFileName = value;
                    }
                    break;
                case "cachefilename":
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        settings.CacheFileName = value;
                    }
                    break;
                case "logdirectory":
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        settings.LogDirectory = value;
                    }
                    break;
                case "windowwidth":
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var w))
                    {
                        settings.WindowWidth = Math.Max(MinWindowWidth, w);
                    }
                    break;
                case "windowheight":
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var h))
                    {
                        settings.WindowHeight = Math.Max(MinWindowHeight, h);
                    }
                    break;
            }
        }

        settings.EnsureFolders();
        settings.WindowWidth = Math.Max(MinWindowWidth, settings.WindowWidth);
        settings.WindowHeight = Math.Max(MinWindowHeight, settings.WindowHeight);
        return settings;
    }

    private static string GetDefaultSettingsPath() => Path.Combine(AppContext.BaseDirectory, SettingsFileName);

    private void EnsureFolders()
    {
        if (!string.IsNullOrWhiteSpace(LogDirectory))
        {
            Directory.CreateDirectory(LogDirectory);
        }
    }
}
