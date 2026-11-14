#pragma warning disable MA0182 // the Agents page hosts the view in the U1 page PR
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
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

    private TerminalViewViewModel? _asking;

    public TerminalView()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        DataContextChanged += (_, _) => FollowStopAsk();
        Pane.FitChanged += (_, e) => PaneFitChanged?.Invoke(this, e);
    }

    /// <summary>Raised for a key that goes to the agent (the pane has focus and the key is not a chord).</summary>
    internal event EventHandler<TerminalKeyEventArgs>? PaneKeyPressed;

    /// <summary>Raised when the person pastes into the pane and the clipboard holds text.</summary>
    internal event EventHandler<TerminalPasteEventArgs>? PanePasted;

    /// <summary>Reads the clipboard's text; the window's clipboard unless a test swaps it.</summary>
    internal Func<Task<string?>> ReadClipboard
    {
        get => field ??= ReadWindowClipboard;
        set;
    }

    /// <summary>How many rows the pane is scrolled back from the live screen (0 at the bottom).</summary>
    internal int PaneScrollOffset => Pane.ScrollOffset;

    /// <summary>Raised when the drawing area changes the columns and rows it fits.</summary>
    internal event EventHandler<TerminalSizeEventArgs>? PaneFitChanged;

    /// <summary>Raised by the Transcript button.</summary>
    internal event EventHandler? TranscriptRequested;

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
                PaneKey(name, e.KeySymbol);
                break;
            case TerminalKeyRoute.Page:
            default:
                break;
        }
    }

    // A key in the pane: Shift+PageUp and Shift+PageDown scroll its own scrollback one screen and send nothing; a paste sends the clipboard's text
    // as one paste; any other key snaps the pane back to the bottom and goes to the agent.
    private void PaneKey(string name, string? symbol)
    {
        if (string.Equals(name, "Shift+PageUp", StringComparison.Ordinal))
        {
            Pane.ScrollBy(Pane.PageRows);
        }
        else if (string.Equals(name, "Shift+PageDown", StringComparison.Ordinal))
        {
            Pane.ScrollBy(-Pane.PageRows);
        }
        else if (TerminalKeyNames.IsPaste(name))
        {
            _ = PasteClipboard();
        }
        else
        {
            Pane.ScrollOffset = 0;
            PaneKeyPressed?.Invoke(this, new TerminalKeyEventArgs(name, symbol));
        }
    }

    private async Task PasteClipboard()
    {
        var text = await ReadClipboard().ConfigureAwait(true);
        if (!string.IsNullOrEmpty(text))
        {
            Pane.ScrollOffset = 0;
            PanePasted?.Invoke(this, new TerminalPasteEventArgs(text));
        }
    }

    private async Task<string?> ReadWindowClipboard() =>
        TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard ? await clipboard.TryGetTextAsync().ConfigureAwait(true) : null;

    private void OnCardButton(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: TerminalCardButtonItem item } && DataContext is TerminalViewViewModel vm)
        {
            vm.Press(item.Button);
        }
    }

    private void OnClose(object? sender, RoutedEventArgs e) => (DataContext as TerminalViewViewModel)?.Close();

    private void OnTranscript(object? sender, RoutedEventArgs e) => TranscriptRequested?.Invoke(this, EventArgs.Empty);

    private async void OnStop(object? sender, RoutedEventArgs e)
    {
        if (DataContext is TerminalViewViewModel vm)
        {
            try
            {
                _ = await vm.PressStop(CancellationToken.None);
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidOperationException or OperationCanceledException)
            {
                // the stop failed after the stopper's own handling: the view model's line says so; an async void must not throw on the UI thread
            }
        }
    }

    private async void OnInterrupt(object? sender, RoutedEventArgs e)
    {
        if (DataContext is TerminalViewViewModel vm)
        {
            _ = await vm.PressInterrupt(CancellationToken.None);
        }
    }

    // The first Stop press asks for 2 s; the view ticks the view model once the window is over so the label goes back.
    private void FollowStopAsk()
    {
        _asking?.StopArmed -= OnStopArmed;
        _asking = DataContext as TerminalViewViewModel;
        _asking?.StopArmed += OnStopArmed;
    }

    private void OnStopArmed(object? sender, EventArgs e) =>
        DispatcherTimer.RunOnce(() => (DataContext as TerminalViewViewModel)?.Tick(), TerminalViewViewModel.StopWindow + TimeSpan.FromMilliseconds(100));
}
