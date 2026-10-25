using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Context.Hits;

namespace Aw.Sindri.Ui.Views.Context.Hits;

/// <summary>The hits of a search beside the map: arrow keys move between rows, Enter or a click focuses the row's node.</summary>
internal sealed partial class MapHitsList : UserControl
{
    public MapHitsList()
    {
        InitializeComponent();
    }

    // Enter on the row the arrow keys are on: it is chosen again, so the camera goes back to it.
    private void OnKey(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is MapHitsViewModel { Selected: { } row } hits)
        {
            e.Handled = true;
            hits.Choose(row);
        }
    }

    // A click on the row that is already chosen changes no selection, so the row says it was chosen.
    private void OnTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: MapHitRow row } && DataContext is MapHitsViewModel hits)
        {
            hits.Choose(row);
        }
    }
}
