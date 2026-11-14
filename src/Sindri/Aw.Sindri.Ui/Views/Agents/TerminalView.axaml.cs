#pragma warning disable MA0182 // the Agents page hosts the view in the U1 page PR
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.Terminal;
using Aw.Sindri.Ui.ViewModels.Agents;

namespace Aw.Sindri.Ui.Views.Agents;

/// <summary>
/// The terminal view (agents spec §15.3): the list on the left, the live pane on the right, a header and a footer. Its key handler runs on the tunnel, so
/// F6 and the second chord reach Sindri before the pane sees a key. Under 1100 px of width the list is a 56 px column.
/// </summary>
internal sealed partial class TerminalView : UserControl
{
    private const double NarrowBelow = 1100;

    public TerminalView()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        Pane.FitChanged += (_, e) => PaneFitChanged?.Invoke(this, e);
    }

    /// <summary>Raised for a key that goes to the agent (the pane has focus and the key is not a chord).</summary>
    internal event EventHandler<TerminalKeyEventArgs>? PaneKeyPressed;

    /// <summary>Raised when the drawing area changes the columns and rows it fits.</summary>
    internal event EventHandler<TerminalSizeEventArgs>? PaneFitChanged;

    /// <summary>Raised by the Transcript button.</summary>
    internal event EventHandler? TranscriptRequested;

    /// <summary>Raised by the Stop… button.</summary>
    internal event EventHandler? StopRequested;

    /// <summary>The pane the socket's frames are drawn into.</summary>
    internal ITerminalPane PaneDrawing => Pane;

    /// <summary>The list column's content (the page's row list).</summary>
    internal object? ListContent
    {
        get => ListHost.Content;
        set => ListHost.Content = value;
    }

    /// <summary>False when the page keeps the list beside this view: the view then draws only the pane, its header and its footer.</summary>
    internal bool ShowsList
    {
        get;
        set
        {
            field = value;
            ListFrame.IsVisible = value;
            Body.ColumnDefinitions[0].Width = value ? new GridLength(Bounds.Width < NarrowBelow ? 56 : 360) : new GridLength(0);
        }
    } = true;

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        Body.ColumnDefinitions[0].Width = ShowsList ? new GridLength(e.NewSize.Width < NarrowBelow ? 56 : 360) : new GridLength(0);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not TerminalViewViewModel vm)
        {
            return;
        }

        var name = TerminalKeyNames.From(e.Key, e.KeyModifiers);
        switch (vm.Route(name))
        {
            case TerminalKeyRoute.Handled:
                e.Handled = true;
                break;
            case TerminalKeyRoute.Pane:
                e.Handled = true;
                PaneKeyPressed?.Invoke(this, new TerminalKeyEventArgs(name, e.KeySymbol));
                break;
            case TerminalKeyRoute.Page:
            default:
                break;
        }
    }

    private void OnCardButton(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: TerminalCardButtonItem item } && DataContext is TerminalViewViewModel vm)
        {
            vm.Press(item.Button);
        }
    }

    private void OnClose(object? sender, RoutedEventArgs e) => (DataContext as TerminalViewViewModel)?.Close();

    private void OnTranscript(object? sender, RoutedEventArgs e) => TranscriptRequested?.Invoke(this, EventArgs.Empty);

    private void OnStop(object? sender, RoutedEventArgs e) => StopRequested?.Invoke(this, EventArgs.Empty);
}
