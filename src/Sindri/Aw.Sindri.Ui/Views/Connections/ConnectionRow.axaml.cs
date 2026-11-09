using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Connections;

namespace Aw.Sindri.Ui.Views.Connections;

/// <summary>One connection: a title click is passed up to the page as a routed event.</summary>
internal sealed partial class ConnectionRow : UserControl
{
    /// <summary>A title click: open the connection's sheet.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> OpenEvent = RoutedEvent.Register<ConnectionRow, RoutedEventArgs>(nameof(OpenEvent), RoutingStrategies.Bubble);

    public ConnectionRow()
    {
        InitializeComponent();
    }

    /// <summary>The row's connection.</summary>
    internal ConnectionRowViewModel? Connection => DataContext as ConnectionRowViewModel;

    private void OnOpen(object? sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(OpenEvent, this));
}
