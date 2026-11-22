using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Views.Context.Whole;

/// <summary>The whole map's accessible list and its handlers (<see cref="WholeMapView"/>, continued).</summary>
internal sealed partial class WholeMapView
{
    // The list's items carry their name for a screen reader and the driver.
    private void OnItemPrepared(object? sender, ContainerPreparedEventArgs e)
    {
        if (e.Container is ListBoxItem container && container.DataContext is MapItemViewModel item)
        {
            AutomationProperties.SetName(container, item.Name);
        }
    }

    // Enter on an item is a click on the map's node or feature.
    private void OnItemsKey(object? sender, KeyEventArgs e)
    {
        var item = (e.Source as Control)?.DataContext as MapItemViewModel ?? WholeItems.SelectedItem as MapItemViewModel;   // the focused row first: the selection can lag it
        if (e.Key == Key.Enter && item is not null)
        {
            _map?.ChooseItem(item);
            e.Handled = true;
        }
    }

    private void OnItemsTapped(object? sender, TappedEventArgs e)
    {
        if (WholeItems.SelectedItem is MapItemViewModel item)
        {
            _map?.ChooseItem(item);
        }
    }

    private void OnAreaListTapped(object? sender, TappedEventArgs e)
    {
        if (WholeAreaList.SelectedItem is MapItemViewModel item)
        {
            _map?.ChooseItem(item);
        }
    }

    private void OnListRetry(object? sender, RoutedEventArgs e) => _map?.RetryList();
}
