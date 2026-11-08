using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Takeover;

namespace Aw.Sindri.Ui.Views.Components;

/// <summary>The sandbox banner of a taken-over run; Dismiss hides it until the next takeover.</summary>
#pragma warning disable MA0182 // why: PR 17's attach and take over control uses it
internal sealed partial class TakeoverBannerView : UserControl
{
    public TakeoverBannerView()
    {
        InitializeComponent();
    }

    private void OnDismiss(object? sender, RoutedEventArgs e) => (DataContext as TakeoverBannerViewModel)?.Dismiss();
}
#pragma warning restore MA0182
