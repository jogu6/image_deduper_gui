using System.Collections.Specialized;
using ImageDeduper.App.Services;
using ImageDeduper.App.ViewModels;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;

namespace ImageDeduper.App.Views;

public sealed partial class MainPage : Page
{
    private bool _shouldAutoScroll = true;
    private bool _userInterruptedScroll;

    public MainViewModel ViewModel { get; }

    public MainPage()
    {
        this.InitializeComponent();
        ViewModel = new MainViewModel(new DeduperController(App.Settings), new FolderPickerService(), DispatcherQueue.GetForCurrentThread());
        DataContext = ViewModel;
        ViewModel.Logs.CollectionChanged += OnLogsCollectionChanged;
        LogsScrollViewer.SizeChanged += OnLogsSizeChanged;
        LogsScrollViewer.ViewChanged += OnLogsViewChanged;
        LogsScrollViewer.PointerPressed += OnLogsPointerInteraction;
        LogsScrollViewer.PointerWheelChanged += OnLogsPointerWheelChanged;
    }


    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ViewModel.Logs.CollectionChanged -= OnLogsCollectionChanged;
        ViewModel.Dispose();
        LogsScrollViewer.SizeChanged -= OnLogsSizeChanged;
        LogsScrollViewer.ViewChanged -= OnLogsViewChanged;
        LogsScrollViewer.PointerPressed -= OnLogsPointerInteraction;
        LogsScrollViewer.PointerWheelChanged -= OnLogsPointerWheelChanged;
    }

    private void OnLogsViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        if (LogsScrollViewer is null)
        {
            return;
        }

        var verticalOffset = LogsScrollViewer.VerticalOffset;
        var maximumOffset = LogsScrollViewer.ScrollableHeight;
        var atBottom = maximumOffset - verticalOffset < 4.0;

        if (_userInterruptedScroll)
        {
            _shouldAutoScroll = atBottom;
            if (atBottom)
            {
                _userInterruptedScroll = false;
            }
        }
        else
        {
            _shouldAutoScroll = true;
        }
    }

    private void OnLogsSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.PreviousSize.Height == 0 && e.NewSize.Height > 0)
        {
            _shouldAutoScroll = true;
        }
    }

    private void OnLogsPointerInteraction(object sender, PointerRoutedEventArgs e) => _userInterruptedScroll = true;

    private void OnLogsPointerWheelChanged(object sender, PointerRoutedEventArgs e) => _userInterruptedScroll = true;


    private async void OnSettingsClicked(object sender, RoutedEventArgs e)
    {
        await ViewModel.ShowSettingsAsync(this.XamlRoot);
    }

    private void OnLogsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add || LogsScrollViewer is null || !_shouldAutoScroll)
        {
            return;
        }

        LogsScrollViewer.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            LogsScrollViewer.UpdateLayout();
            LogsScrollViewer.ChangeView(null, double.MaxValue, null);
        });
    }
}
