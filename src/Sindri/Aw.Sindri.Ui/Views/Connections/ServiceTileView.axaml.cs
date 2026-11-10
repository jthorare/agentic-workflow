using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Connections;

namespace Aw.Sindri.Ui.Views.Connections;

#pragma warning disable MA0182 // why: ConnectionsPage.axaml places it in a template, which the analyzer cannot see
/// <summary>A service tile: its buttons call the tile's view model.</summary>
internal sealed partial class ServiceTileView : UserControl
{
    public ServiceTileView()
    {
        InitializeComponent();
    }

    private ServiceTileViewModel? Tile => DataContext as ServiceTileViewModel;

    private void OnPrimary(object? sender, RoutedEventArgs e) => _ = Tile?.Primary(CancellationToken.None);

    private void OnHelp(object? sender, RoutedEventArgs e) => _ = Tile?.OpenHelp(CancellationToken.None);

    private void OnCancel(object? sender, RoutedEventArgs e) => Tile?.Cancel();

    private void OnSignIn(object? sender, RoutedEventArgs e) => _ = Tile?.SignInAgain(CancellationToken.None);

    // The field opens holding the current label, focused and selected, so typing replaces it.
    private void OnRename(object? sender, RoutedEventArgs e)
    {
        Tile?.StartRename();
        Dispatcher.UIThread.Post(
            () =>
            {
                _ = ServiceTileRenameField.Focus(NavigationMethod.Tab);
                ServiceTileRenameField.SelectAll();
            },
            DispatcherPriority.Loaded);
    }

    private void OnRenameSave(object? sender, RoutedEventArgs e) => _ = Tile?.Rename(CancellationToken.None);

    private void OnRenameCancel(object? sender, RoutedEventArgs e) => Tile?.CancelRename();

    // Enter saves and Escape cancels, as in every inline field of the app.
    private void OnRenameKey(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            _ = Tile?.Rename(CancellationToken.None);
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Tile?.CancelRename();
        }
    }

    private void OnDisconnect(object? sender, RoutedEventArgs e) => _ = Tile?.Disconnect(CancellationToken.None);
}
#pragma warning restore MA0182
