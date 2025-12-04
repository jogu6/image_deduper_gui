using ImageDeduper.Core.Localization;

namespace ImageDeduper.App.ViewModels;

public sealed class SettingsTextResources
{
    public SettingsTextResources(Translator translator)
    {
        Title = translator.T("settings.title");
        SaveButton = translator.T("settings.save");
        CancelButton = translator.T("settings.cancel");
        SsimLabel = translator.T("settings.ssim_label");
        SsimDescription = translator.T("settings.ssim_desc");
        PhashLabel = translator.T("settings.phash_label");
        PhashDescription = translator.T("settings.phash_desc");
        LanguageLabel = translator.T("settings.lang_label");
        LanguageDescription = translator.T("settings.lang_desc");
        FormatsTitle = translator.T("settings.formats_title");
        FormatsDescription = translator.T("settings.formats_desc");
    }

    public string Title { get; }
    public string SaveButton { get; }
    public string CancelButton { get; }
    public string SsimLabel { get; }
    public string SsimDescription { get; }
    public string PhashLabel { get; }
    public string PhashDescription { get; }
    public string LanguageLabel { get; }
    public string LanguageDescription { get; }
    public string FormatsTitle { get; }
    public string FormatsDescription { get; }
}
