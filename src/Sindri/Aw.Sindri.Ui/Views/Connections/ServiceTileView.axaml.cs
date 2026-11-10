using Avalonia.Controls;
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

    private void OnConnect(object? sender, RoutedEventArgs e) => _ = Tile?.Connect(CancellationToken.None);

    private void OnCancel(object? sender, RoutedEventArgs e) => Tile?.Cancel();

    private void OnSignIn(object? sender, RoutedEventArgs e) => _ = Tile?.SignInAgain(CancellationToken.None);

    private void OnDisconnect(object? sender, RoutedEventArgs e) => _ = Tile?.Disconnect(CancellationToken.None);
}
#pragma warning restore MA0182
