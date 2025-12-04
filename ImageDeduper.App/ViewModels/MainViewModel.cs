using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using ImageDeduper.App.Dialogs;
using ImageDeduper.App.Services;
using ImageDeduper.Core.Configuration;
using ImageDeduper.Core.Models;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Globalization;
using System.Threading.Tasks;
using ImageDeduper.Core.Localization;

namespace ImageDeduper.App.ViewModels;

public sealed class MainViewModel : ObservableObject, IDisposable
{
    private readonly DeduperController _controller;
    private readonly IFolderPickerService _folderPicker;
    private readonly DispatcherQueue _dispatcher;
    private readonly Stopwatch _stopwatch = new();
    private ProgressPhase _currentPhase = ProgressPhase.None;

    private string _selectedFolder = string.Empty;
    private bool _isRunning;
    private double _progressValue;
    private string _phaseText = string.Empty;
    private string _progressDetail = "0 / 0";
    private string _progressPercentText = "0%";
    private string _etaText = "-";
    private string _statusMessage = string.Empty;
    private int _totalSources;
    private int _processedBase;
    private int _movedDuplicates;
    private string _elapsedText = "00:00:00";
    private readonly TimeSpan _progressUpdateInterval = TimeSpan.FromMilliseconds(120);
    private DateTime _lastProgressUpdate = DateTime.MinValue;
    private bool _hasResumeFile;
    private bool _resumePromptVisible;
    private bool _resumeFromCheckpoint = true;
    private bool _suppressNextResumePrompt;
    private Translator _uiStrings;

    public MainViewModel(DeduperController controller, IFolderPickerService folderPicker, DispatcherQueue dispatcher)
    {
        _controller = controller;
        _folderPicker = folderPicker;
        _dispatcher = dispatcher;
        _uiStrings = new Translator(_controller.Settings.Language);
        UiText = new UiTextResources(_uiStrings);

        BrowseFolderCommand = new AsyncRelayCommand(BrowseFolderAsync, () => !IsRunning);
        StartCommand = new AsyncRelayCommand(StartAsync, CanStart);
        StopCommand = new RelayCommand(Stop, () => IsRunning);
        KeepResumeCommand = new AsyncRelayCommand(() => HandleResumeSelectionAsync(true), () => ResumePromptVisible && !IsRunning);
        DiscardResumeCommand = new AsyncRelayCommand(() => HandleResumeSelectionAsync(false), () => ResumePromptVisible && !IsRunning);

        Logs = new ObservableCollection<LogEntryViewModel>();

        _controller.ProgressChanged += HandleProgressChanged;
        _controller.LogReceived += HandleLogReceived;
        _controller.StatsUpdated += HandleStatsUpdated;

        UpdatePhase(_currentPhase);
        StatusMessage = _uiStrings.T("ui.status_select_folder");
    }

    public ObservableCollection<LogEntryViewModel> Logs { get; }

    public string SelectedFolder
    {
        get => _selectedFolder;
        set
        {
            if (SetProperty(ref _selectedFolder, value))
            {
                RefreshCommands();
                CheckResumeFile();
                OnPropertyChanged(nameof(SelectedFolderDisplay));
            }
        }
    }

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (SetProperty(ref _isRunning, value))
            {
                RefreshCommands();
                UpdateResumeCommands();
                OnPropertyChanged(nameof(IsStartEnabled));
            }
        }
    }

    public double ProgressValue
    {
        get => _progressValue;
        private set => SetProperty(ref _progressValue, value);
    }

    public string PhaseText
    {
        get => _phaseText;
        private set => SetProperty(ref _phaseText, value);
    }

    public string ProgressDetail
    {
        get => _progressDetail;
        private set => SetProperty(ref _progressDetail, value);
    }

    public string ProgressPercentText
    {
        get => _progressPercentText;
        private set => SetProperty(ref _progressPercentText, value);
    }

    public string EtaText
    {
        get => _etaText;
        private set => SetProperty(ref _etaText, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public int TotalSources
    {
        get => _totalSources;
        private set => SetProperty(ref _totalSources, value);
    }

    public int ProcessedBase
    {
        get => _processedBase;
        private set => SetProperty(ref _processedBase, value);
    }

    public int MovedDuplicates
    {
        get => _movedDuplicates;
        private set => SetProperty(ref _movedDuplicates, value);
    }

    public string ElapsedText
    {
        get => _elapsedText;
        private set => SetProperty(ref _elapsedText, value);
    }

    public AsyncRelayCommand BrowseFolderCommand { get; }
    public AsyncRelayCommand StartCommand { get; }
    public RelayCommand StopCommand { get; }
    public AsyncRelayCommand KeepResumeCommand { get; }
    public AsyncRelayCommand DiscardResumeCommand { get; }
    public UiTextResources UiText { get; }
    public bool IsStartEnabled => CanStart() && !ResumePromptVisible;

    public string SelectedFolderDisplay => string.IsNullOrWhiteSpace(SelectedFolder) ? UiText.NoFolderSelected : SelectedFolder;

    public bool HasResumeFile
    {
        get => _hasResumeFile;
        private set
        {
            if (SetProperty(ref _hasResumeFile, value))
            {
                UpdateResumeCommands();
            }
        }
    }

    public bool ResumePromptVisible
    {
        get => _resumePromptVisible;
        private set
        {
            if (SetProperty(ref _resumePromptVisible, value))
            {
                UpdateResumeCommands();
                OnPropertyChanged(nameof(IsStartEnabled));
            }
        }
    }

    public bool ResumeFromCheckpoint
    {
        get => _resumeFromCheckpoint;
        set
        {
            if (SetProperty(ref _resumeFromCheckpoint, value))
            {
                OnPropertyChanged(nameof(IsStartEnabled));
            }
        }
    }

    public async Task ShowSettingsAsync(XamlRoot xamlRoot)
    {
        if (IsRunning)
        {
            return;
        }

        var dialog = new SettingsDialog
        {
            XamlRoot = xamlRoot,
            ViewModel = new SettingsViewModel(_controller.Settings, _uiStrings)
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary && dialog.ViewModel is not null)
        {
            dialog.ViewModel.ApplyTo(_controller.Settings);
            ReloadLocalization();
            StatusMessage = _uiStrings.T("ui.status_settings_saved");
        }
    }

    private async Task BrowseFolderAsync()
    {
        var folder = await _folderPicker.PickFolderAsync().ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            await _dispatcher.EnqueueAsync(() => SelectedFolder = folder);
        }
    }

    private async Task StartAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedFolder))
        {
            StatusMessage = _uiStrings.T("ui.status_pick_folder");
            return;
        }

        try
        {
            Logs.Clear();
            ProgressValue = 0;
            ProgressPercentText = "0%";
            _currentPhase = ProgressPhase.None;
            UpdatePhase(ProgressPhase.None);
            EtaText = _uiStrings.T("status.estimating");
            StatusMessage = _uiStrings.T("ui.status_processing");
            ElapsedText = "00:00:00";
            _lastProgressUpdate = DateTime.MinValue;
            _stopwatch.Restart();
            if (HasResumeFile && !ResumeFromCheckpoint)
            {
                DeleteCheckpointFiles();
                HasResumeFile = false;
            }

            IsRunning = true;
            await _controller.StartAsync(SelectedFolder);
            StatusMessage = _uiStrings.T("ui.status_completed");
        }
        catch (OperationCanceledException)
        {
            StatusMessage = _uiStrings.T("ui.status_canceled");
        }
        catch (Exception ex)
        {
            StatusMessage = _uiStrings.T("ui.status_error", ("message", ex.Message));
        }
        finally
        {
            _stopwatch.Stop();
            var suppressPrompt = _suppressNextResumePrompt;
            _suppressNextResumePrompt = false;
            await _dispatcher.EnqueueAsync(() =>
            {
                IsRunning = false;
                ProgressValue = 0;
                ProgressPercentText = "0%";
                EtaText = "-";
                UpdatePhase(ProgressPhase.None);
                CheckResumeFile(suppressPrompt);
            });
        }
    }

    private void Stop()
    {
        _suppressNextResumePrompt = true;
        _controller.Cancel();
    }

    private bool CanStart() => !IsRunning && !string.IsNullOrWhiteSpace(SelectedFolder);

    private void RefreshCommands()
    {
        BrowseFolderCommand.RaiseCanExecuteChanged();
        StartCommand.RaiseCanExecuteChanged();
        StopCommand.RaiseCanExecuteChanged();
        UpdateResumeCommands();
        OnPropertyChanged(nameof(IsStartEnabled));
    }

    private void HandleProgressChanged(object? sender, ProgressUpdate e)
    {
        _dispatcher.TryEnqueue(() =>
        {
            var now = DateTime.UtcNow;
            var phaseChanged = _currentPhase != e.Phase;
            if (phaseChanged)
            {
                _currentPhase = e.Phase;
                ProgressValue = 0;
            }

            var shouldUpdate = phaseChanged
                               || now - _lastProgressUpdate >= _progressUpdateInterval
                               || e.Completed == 0
                               || e.Completed >= e.Total;

            if (!shouldUpdate)
            {
                return;
            }

            _lastProgressUpdate = now;
            UpdatePhase(e.Phase);
            var ratio = e.Total <= 0 ? 0 : (double)e.Completed / e.Total;
            ProgressValue = ratio * 100;
            var completedText = e.Completed.ToString("N0", CultureInfo.InvariantCulture);
            var totalText = e.Total.ToString("N0", CultureInfo.InvariantCulture);
            ProgressDetail = $"{completedText}/{totalText}";
            EtaText = e.EtaText;
            ProgressPercentText = $"{ratio * 100:0.0}%";
        });
    }

    private void UpdatePhase(ProgressPhase phase)
    {
        var phaseNameKey = phase switch
        {
            ProgressPhase.Loading => "ui.phase_loading",
            ProgressPhase.Comparing => "ui.phase_comparing",
            _ => "ui.phase_idle"
        };

        var phaseName = _uiStrings.T(phaseNameKey);
        var orderedPhases = new[] { ProgressPhase.Loading, ProgressPhase.Comparing };
        var index = Array.IndexOf(orderedPhases, phase);
        if (index >= 0)
        {
            var displayIndex = (index + 1).ToString(CultureInfo.InvariantCulture);
            var displayTotal = orderedPhases.Length.ToString(CultureInfo.InvariantCulture);
            PhaseText = _uiStrings.T("ui.phase_header_with_count", ("index", displayIndex), ("total", displayTotal), ("phase", phaseName));
        }
        else
        {
            PhaseText = _uiStrings.T("ui.phase_header", ("phase", phaseName));
        }
    }

    private void HandleLogReceived(object? sender, ImageDeduper.Core.Logging.LogEntry e)
    {
        _dispatcher.TryEnqueue(() =>
        {
            Logs.Add(new LogEntryViewModel(e));
        });
    }

    private void HandleStatsUpdated(object? sender, StatsSnapshot e)
    {
        _dispatcher.TryEnqueue(() =>
        {
            TotalSources = e.TotalSources;
            ProcessedBase = e.ProcessedBase;
            MovedDuplicates = e.MovedDuplicates;
            ElapsedText = _stopwatch.Elapsed.ToString(@"hh\:mm\:ss");
        });
    }

    public void Dispose()
    {
        _controller.ProgressChanged -= HandleProgressChanged;
        _controller.LogReceived -= HandleLogReceived;
        _controller.StatsUpdated -= HandleStatsUpdated;
        _controller.Dispose();
    }

    private void CheckResumeFile(bool suppressPrompt = false)
    {
        if (string.IsNullOrWhiteSpace(SelectedFolder))
        {
            HasResumeFile = false;
            ResumePromptVisible = false;
            return;
        }

        var resumePath = Path.Combine(SelectedFolder, _controller.Settings.ResumeFileName);
        var cachePath = Path.Combine(SelectedFolder, _controller.Settings.CacheFileName);
        var exists = IsRunning && ResumeFromCheckpoint
            ? HasResumeFile
            : File.Exists(resumePath) || File.Exists(cachePath);
        HasResumeFile = exists;
        ResumeFromCheckpoint = exists;
        ResumePromptVisible = exists && !suppressPrompt;
    }

    private void DeleteCheckpointFiles()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(SelectedFolder))
            {
                return;
            }

            var resumePath = Path.Combine(SelectedFolder, _controller.Settings.ResumeFileName);
            var cachePath = Path.Combine(SelectedFolder, _controller.Settings.CacheFileName);
            var loadingCheckpointPath = Path.Combine(SelectedFolder, _controller.Settings.LoadingCheckpointFileName);
            if (File.Exists(resumePath))
            {
                File.Delete(resumePath);
            }

            if (File.Exists(cachePath))
            {
                File.Delete(cachePath);
            }

            if (File.Exists(loadingCheckpointPath))
            {
                File.Delete(loadingCheckpointPath);
            }

            ResumeFromCheckpoint = false;
        }
        catch (Exception ex)
        {
            StatusMessage = _uiStrings.T("ui.status_resume_delete_failed", ("message", ex.Message));
        }
    }

    private async Task HandleResumeSelectionAsync(bool resume)
    {
        ResumeFromCheckpoint = resume;
        if (!resume)
        {
            DeleteCheckpointFiles();
            HasResumeFile = false;
            StatusMessage = _uiStrings.T("ui.status_checkpoint_cleared");
        }
        else
        {
            StatusMessage = _uiStrings.T("ui.status_resume_ready");
        }

        ResumePromptVisible = false;

        if (!IsRunning && !string.IsNullOrWhiteSpace(SelectedFolder))
        {
            await StartAsync();
        }
    }

    private void ReloadLocalization()
    {
        _uiStrings = new Translator(_controller.Settings.Language);
        UiText.Update(_uiStrings);
        UpdatePhase(_currentPhase);
        OnPropertyChanged(nameof(SelectedFolderDisplay));
        if (!IsRunning && string.IsNullOrWhiteSpace(SelectedFolder))
        {
            StatusMessage = _uiStrings.T("ui.status_select_folder");
        }
    }

    private void UpdateResumeCommands()
    {
        KeepResumeCommand?.RaiseCanExecuteChanged();
        DiscardResumeCommand?.RaiseCanExecuteChanged();
    }
}

internal static class DispatcherQueueExtensions
{
    public static Task EnqueueAsync(this DispatcherQueue dispatcher, Action action)
    {
        var tcs = new TaskCompletionSource<bool>();
        dispatcher.TryEnqueue(() =>
        {
            try
            {
                action();
                tcs.SetResult(true);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        return tcs.Task;
    }
}
