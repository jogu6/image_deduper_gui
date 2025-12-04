using System;
using System.Globalization;
using ImageDeduper.App.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace ImageDeduper.App.Dialogs;

public sealed partial class SettingsDialog : ContentDialog
{
    public SettingsDialog()
    {
        this.InitializeComponent();
    }

    public SettingsViewModel? ViewModel
    {
        get => DataContext as SettingsViewModel;
        set => DataContext = value;
    }

    private void OnSsimValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (double.IsNaN(args.NewValue))
        {
            return;
        }

        var clamped = Math.Clamp(args.NewValue, 0.5, 1.0);
        var rounded = Math.Round(clamped, 2, MidpointRounding.AwayFromZero);
        if (Math.Abs(sender.Value - rounded) > 0.0001)
        {
            sender.Value = rounded;
        }
        sender.Text = rounded.ToString("0.00", CultureInfo.InvariantCulture);
    }
}
