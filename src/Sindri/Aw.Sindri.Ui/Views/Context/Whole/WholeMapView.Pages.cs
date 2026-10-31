using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Views.Context.Whole;

/// <summary>The Pages and actions section's buttons (<see cref="WholeMapView"/>, continued, pages spec §3.4, §3.6 "Keyboard").</summary>
internal sealed partial class WholeMapView
{
    // An action's name opens its node panel.
    private void OnPagesAction(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: PageActionRowViewModel row })
        {
            _map?.ChooseNode(row.Key);
        }
    }

    // Try again reads the concept's map again; focus goes to the section head.
    private void OnPagesRetry(object? sender, RoutedEventArgs e)
    {
        _map?.RetryPages();
        _ = WholePagesHead.Focus(NavigationMethod.Tab);
    }

    // Show all lists the rest; focus goes to the first newly shown row.
    private void OnPagesShowAll(object? sender, RoutedEventArgs e)
    {
        if (_map is null)
        {
            return;
        }

        _map.Pages.ShowAll();
        Dispatcher.UIThread.Post(() => _ = WholePagesRows.ContainerFromIndex(PagesSectionViewModel.Cap)?.GetVisualDescendants().OfType<Button>().FirstOrDefault()?.Focus(NavigationMethod.Tab), DispatcherPriority.Loaded);
    }
}
