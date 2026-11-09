using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Connections;

namespace Aw.Sindri.Ui.Views.Connections;

#pragma warning disable MA0182 // why: ConnectionsPage.axaml places it, which the analyzer cannot see
/// <summary>The Add connection form: Add sends once, Close drops anything pasted.</summary>
internal sealed partial class AddConnectionForm : UserControl
{
    public AddConnectionForm()
    {
        InitializeComponent();
    }

    private void OnSend(object? sender, RoutedEventArgs e) => _ = (DataContext as AddConnectionViewModel)?.Send(CancellationToken.None);

    private void OnClose(object? sender, RoutedEventArgs e) => (DataContext as AddConnectionViewModel)?.Close();
}
#pragma warning restore MA0182
