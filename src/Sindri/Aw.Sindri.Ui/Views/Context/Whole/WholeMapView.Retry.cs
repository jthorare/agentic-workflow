using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Views.Context.Whole;

/// <summary>The node panel's fix buttons (<see cref="WholeMapView"/>, continued, #3060): each failed state's one button calls its view model.</summary>
internal sealed partial class WholeMapView
{
    private void OnNodePanelRetry(object? sender, RoutedEventArgs e) => _ = _map?.Panel.Retry();

    private void OnNodePanelLaunchRetry(object? sender, RoutedEventArgs e) => _ = _map?.Panel.RetryLaunch();

    private void OnNodeSectionRetry(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: NodeSectionViewModel section })
        {
            _ = section.Retry();
        }
    }
}
