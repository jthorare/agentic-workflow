using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Connections;

namespace Aw.Sindri.Ui.Views.Connections;

#pragma warning disable MA0182 // why: ShellWindow.axaml places it in a Deferred template, which the analyzer cannot see
/// <summary>The Connections page: a title click on a row opens the connection's sheet; Add connection shows the form.</summary>
internal sealed partial class ConnectionsPage : UserControl
{
    public ConnectionsPage()
    {
        InitializeComponent();
        AddHandler(ConnectionRow.OpenEvent, OnOpen);
    }

    private void OnAdd(object? sender, RoutedEventArgs e) => (DataContext as ConnectionsPageViewModel)?.Add.Open();

    private void OnOpen(object? sender, RoutedEventArgs e)
    {
        if (e.Source is ConnectionRow { Connection: { } connection } && DataContext is ConnectionsPageViewModel page)
        {
            page.RequestOpen(connection.Link);
        }
    }
}
#pragma warning restore MA0182
