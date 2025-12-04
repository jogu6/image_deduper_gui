using ImageDeduper.Core.Localization;

namespace ImageDeduper.App.ViewModels;

public sealed class UiTextResources : ObservableObject
{
    private string _commandFolder = string.Empty;
    private string _commandStart = string.Empty;
    private string _commandStop = string.Empty;
    private string _commandSettings = string.Empty;
    private string _sourceFolderTitle = string.Empty;
    private string _resumeDetectedTitle = string.Empty;
    private string _resumeDetectedDescription = string.Empty;
    private string _resumeButtonLabel = string.Empty;
    private string _startOverButtonLabel = string.Empty;
    private string _totalImagesLabel = string.Empty;
    private string _baseProcessedLabel = string.Empty;
    private string _movedLabel = string.Empty;
    private string _elapsedLabel = string.Empty;
    private string _liveLogsTitle = string.Empty;
    private string _footerDescription = string.Empty;
    private string _etaLabel = string.Empty;
    private string _noFolderSelected = string.Empty;

    public UiTextResources(Translator translator)
    {
        Update(translator);
    }

    public string CommandFolder
    {
        get => _commandFolder;
        private set => SetProperty(ref _commandFolder, value);
    }

    public string CommandStart
    {
        get => _commandStart;
        private set => SetProperty(ref _commandStart, value);
    }

    public string CommandStop
    {
        get => _commandStop;
        private set => SetProperty(ref _commandStop, value);
    }

    public string CommandSettings
    {
        get => _commandSettings;
        private set => SetProperty(ref _commandSettings, value);
    }

    public string SourceFolderTitle
    {
        get => _sourceFolderTitle;
        private set => SetProperty(ref _sourceFolderTitle, value);
    }

    public string ResumeDetectedTitle
    {
        get => _resumeDetectedTitle;
        private set => SetProperty(ref _resumeDetectedTitle, value);
    }

    public string ResumeDetectedDescription
    {
        get => _resumeDetectedDescription;
        private set => SetProperty(ref _resumeDetectedDescription, value);
    }

    public string ResumeButtonLabel
    {
        get => _resumeButtonLabel;
        private set => SetProperty(ref _resumeButtonLabel, value);
    }

    public string StartOverButtonLabel
    {
        get => _startOverButtonLabel;
        private set => SetProperty(ref _startOverButtonLabel, value);
    }

    public string TotalImagesLabel
    {
        get => _totalImagesLabel;
        private set => SetProperty(ref _totalImagesLabel, value);
    }

    public string BaseProcessedLabel
    {
        get => _baseProcessedLabel;
        private set => SetProperty(ref _baseProcessedLabel, value);
    }

    public string MovedLabel
    {
        get => _movedLabel;
        private set => SetProperty(ref _movedLabel, value);
    }

    public string ElapsedLabel
    {
        get => _elapsedLabel;
        private set => SetProperty(ref _elapsedLabel, value);
    }

    public string LiveLogsTitle
    {
        get => _liveLogsTitle;
        private set => SetProperty(ref _liveLogsTitle, value);
    }

    public string FooterDescription
    {
        get => _footerDescription;
        private set => SetProperty(ref _footerDescription, value);
    }

    public string EtaLabel
    {
        get => _etaLabel;
        private set => SetProperty(ref _etaLabel, value);
    }

    public string NoFolderSelected
    {
        get => _noFolderSelected;
        private set => SetProperty(ref _noFolderSelected, value);
    }

    public void Update(Translator translator)
    {
        CommandFolder = translator.T("ui.cmd_folder");
        CommandStart = translator.T("ui.cmd_start");
        CommandStop = translator.T("ui.cmd_stop");
        CommandSettings = translator.T("ui.cmd_settings");
        SourceFolderTitle = translator.T("ui.source_folder_title");
        ResumeDetectedTitle = translator.T("ui.resume_detected_title");
        ResumeDetectedDescription = translator.T("ui.resume_detected_desc");
        ResumeButtonLabel = translator.T("ui.resume_button");
        StartOverButtonLabel = translator.T("ui.start_over_button");
        TotalImagesLabel = translator.T("ui.total_images_label");
        BaseProcessedLabel = translator.T("ui.base_processed_label");
        MovedLabel = translator.T("ui.moved_label");
        ElapsedLabel = translator.T("ui.elapsed_label");
        LiveLogsTitle = translator.T("ui.live_logs_title");
        FooterDescription = translator.T("ui.footer_description");
        EtaLabel = translator.T("ui.eta_label");
        NoFolderSelected = translator.T("ui.no_folder_selected");
    }
}
