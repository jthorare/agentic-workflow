using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Views.Context.Whole;

/// <summary>The whole map's drill-in (<see cref="WholeMapView"/>, continued; #3674): the area tiles, the concept tabs, the paged lists and their keys, and focus coming back after Esc.</summary>
internal sealed partial class WholeMapView
{
    // The stage changed size: the window's width decides how Core draws, then the world is arranged again.
    private void Resized()
    {
        _map?.ViewWidth = TopLevel.GetTopLevel(this)?.ClientSize.Width ?? Bounds.Width;
        _map?.ViewStage = (WholeCanvas.FitStage.Width, WholeCanvas.FitStage.Height);

        Arrange();
    }

    // [ and ] turn the pages of a list.
    private bool TurnPage(Key key) => (key == Key.OemOpenBrackets || key == Key.OemCloseBrackets) && _map?.PageKey(next: key == Key.OemCloseBrackets) == true;

    // Esc came back to a list or the tiles: focus goes to the tile or row the person left from.
    private void OnFocusReturned(object? sender, ValueEventArgs<string> e) =>
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            Control? target = this.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.DataContext is AreaCard card && string.Equals(card.Key, e.Value, StringComparison.Ordinal));
            if (target is null && WholeAreaList.Items.OfType<MapItemViewModel>().FirstOrDefault(i => string.Equals(i.Key, e.Value, StringComparison.Ordinal)) is { } row)
            {
                target = WholeAreaList.ContainerFromItem(row);
            }

            _ = target?.Focus(NavigationMethod.Tab);
        });

    private void OnAreaCard(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: AreaCard card })
        {
            _map?.OpenTile(card.Key);
        }
    }

    private void OnConceptTab(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: ConceptTabItem tab })
        {
            _map?.ChooseTab(tab.Key);
        }
    }

    private void OnAreaPrevious(object? sender, RoutedEventArgs e) => _map?.PreviousPage();

    private void OnAreaNext(object? sender, RoutedEventArgs e) => _map?.NextPage();

    private void OnAreaListTapped(object? sender, TappedEventArgs e)
    {
        if (WholeAreaList.SelectedItem is MapItemViewModel item)
        {
            _map?.ChooseItem(item);
        }
    }

    private void OnListRetry(object? sender, RoutedEventArgs e) => _map?.RetryList();
}
