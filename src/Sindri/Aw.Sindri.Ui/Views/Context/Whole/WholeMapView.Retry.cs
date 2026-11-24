using Avalonia.Interactivity;

namespace Aw.Sindri.Ui.Views.Context.Whole;

/// <summary>The node panel's fix buttons (<see cref="WholeMapView"/>, continued, #3060): each failed state's one button calls its view model.</summary>
internal sealed partial class WholeMapView
{
    private void OnNodePanelRetry(object? sender, RoutedEventArgs e) => _ = _map?.Panel.Retry();
}
