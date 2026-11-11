using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Agents;

namespace Aw.Sindri.Ui.Views.Agents;

/// <summary>One live session: it passes a title click and focus arriving up to the page as routed events.</summary>
internal sealed partial class SessionRow : UserControl
{
    /// <summary>A title click: open the session's sheet.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> OpenEvent = RoutedEvent.Register<SessionRow, RoutedEventArgs>(nameof(OpenEvent), RoutingStrategies.Bubble);

    /// <summary>Focus arrived on the row or a control in it.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> SelectEvent = RoutedEvent.Register<SessionRow, RoutedEventArgs>(nameof(SelectEvent), RoutingStrategies.Bubble);

    public SessionRow()
    {
        InitializeComponent();
        AddHandler(GotFocusEvent, (_, _) => RaiseEvent(new RoutedEventArgs(SelectEvent, this)));
    }

    /// <summary>The row's session ref.</summary>
    internal string Key => (DataContext as SessionRowViewModel)?.Ref ?? string.Empty;

    private void OnOpen(object? sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(OpenEvent, this));


    private void OnCopyFocus(object? sender, RoutedEventArgs e) => _ = (DataContext as SessionRowViewModel)?.Focus.Copy(CancellationToken.None);
}
