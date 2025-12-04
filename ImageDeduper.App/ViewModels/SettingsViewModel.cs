using System;
using System.Collections.ObjectModel;
using System.Linq;
using ImageDeduper.Core.Configuration;
using ImageDeduper.Core.Localization;
using Windows.Graphics.Imaging;

namespace ImageDeduper.App.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private const string HeicStoreUrl = "https://apps.microsoft.com/detail/9pmmsr1cgpwg";

    private double _ssimThreshold;
    private int _phashThreshold;
    private string _selectedLanguage = "en";
    private readonly Translator _translator;

    public SettingsTextResources Resources { get; }
    public ObservableCollection<ImageFormatInfo> FormatInfos { get; }

    public SettingsViewModel(AppSettings settings, Translator translator)
    {
        _translator = translator;
        _ssimThreshold = settings.DefaultSsimThreshold;
        _phashThreshold = settings.PHashThreshold;
        _selectedLanguage = settings.Language;
        Resources = new SettingsTextResources(translator);
        FormatInfos = new ObservableCollection<ImageFormatInfo>(BuildFormatInfos());
    }

    public double SsimThreshold
    {
        get => _ssimThreshold;
        set
        {
            var clamped = Math.Clamp(value, 0.5, 1.0);
            var rounded = Math.Round(clamped, 2, MidpointRounding.AwayFromZero);
            SetProperty(ref _ssimThreshold, rounded);
        }
    }

    public int PhashThreshold
    {
        get => _phashThreshold;
        set => SetProperty(ref _phashThreshold, Math.Clamp(value, 0, 64));
    }

    public string SelectedLanguage
    {
        get => _selectedLanguage;
        set => SetProperty(ref _selectedLanguage, value);
    }

    public IReadOnlyList<string> Languages { get; } = new[] { "en", "ja" };

    public void ApplyTo(AppSettings settings)
    {
        settings.DefaultSsimThreshold = SsimThreshold;
        settings.PHashThreshold = PhashThreshold;
        settings.Language = SelectedLanguage;
        settings.Save();
    }

    private IEnumerable<ImageFormatInfo> BuildFormatInfos()
    {
        var availableText = _translator.T("settings.format_available");
        var unavailableText = _translator.T("settings.format_unavailable");
        var storeLinkLabel = _translator.T("settings.store_link_label");
        var heicSupported = CheckHeicSupport();
        var storeUri = heicSupported ? null : new Uri(HeicStoreUrl);

        ImageFormatInfo Create(string nameKey, string extensions, bool available, bool showStoreLink = false)
        {
            var name = _translator.T(nameKey);
            var extLabel = _translator.T("settings.extensions_label", ("ext", extensions));
            var status = available ? availableText : unavailableText;
            return new ImageFormatInfo(name, extLabel, status, showStoreLink, showStoreLink ? storeLinkLabel : null, showStoreLink ? storeUri : null);
        }

        yield return Create("settings.format.jpeg", ".jpg, .jpeg", true);
        yield return Create("settings.format.png", ".png", true);
        yield return Create("settings.format.bmp", ".bmp", true);
        yield return Create("settings.format.gif", ".gif", true);
        yield return Create("settings.format.tiff", ".tiff", true);
        yield return Create("settings.format.webp", ".webp", true);
        yield return Create("settings.format.jfif", ".jfif", true);
        yield return Create("settings.format.heic", ".heic, .heif", heicSupported, !heicSupported);
    }

    private static bool CheckHeicSupport()
    {
        try
        {
            var infos = BitmapDecoder.GetDecoderInformationEnumerator();
            var heicId = BitmapDecoder.HeifDecoderId;
            return infos.Any(info => info.CodecId == heicId);
        }
        catch
        {
            return false;
        }
    }
}
