using System.ComponentModel;
using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Sindri.Ui.ViewModels.Agents;

namespace Aw.Sindri.Ui.Views.Agents;

/// <summary>
/// The Agents page (continued): keyboard focus follows the terminal view's side (#3729 review). When the pane or its card takes the keys (Enter again on the shown row,
/// F6, the second chord, a click in the pane), focus moves into the terminal view, so typed keys reach its key handler and never the page's or the shell's. When the
/// list takes them back, focus returns to the selected row.
/// </summary>
internal sealed partial class AgentsPage
{
    private TerminalViewViewModel? _terminal;
    private TerminalSide _side = TerminalSide.List;

    // Called once from the constructor: F6 and the second chord from the list, and a press on the list while the pane has the keys.
    private void WireTyping()
    {
        AddHandler(KeyDownEvent, OnListChord, RoutingStrategies.Tunnel);
        AddHandler(PointerPressedEvent, OnPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    // The page's view model changed: follow its terminal view's side.
    private void FollowSide()
    {
        _terminal?.PropertyChanged -= OnTerminalChanged;
        _terminal = Page?.Terminal;
        _side = _terminal?.Side ?? TerminalSide.List;
        _terminal?.PropertyChanged += OnTerminalChanged;
    }

    // The view model raises Side on every refresh (each frame), so only a real change moves focus.
    private void OnTerminalChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.Equals(e.PropertyName, nameof(TerminalViewViewModel.Side), StringComparison.Ordinal) || _terminal is not { } view || view.Side == _side)
        {
            return;
        }

        _side = view.Side;
        if (_side is TerminalSide.Pane or TerminalSide.Card)
        {
            Dispatcher.UIThread.Post(() => _ = TerminalPanel.Focus(NavigationMethod.Tab), DispatcherPriority.Loaded);
        }
        else if (Page?.SelectedKey is { Length: > 0 } key)
        {
            Dispatcher.UIThread.Post(() => Focus(new AgentFocusEventArgs(key, toFocusLine: false, onlyIfLost: false)), DispatcherPriority.Loaded);
        }
    }

    // F6, Shift+F6 and the second chord with focus on the list: the terminal view swaps sides before the shell cycles its regions.
    private void OnListChord(object? sender, KeyEventArgs e)
    {
        if (Page is not { IsTerminalOpen: true } page || TerminalPanel.IsKeyboardFocusWithin)
        {
            return;
        }

        var name = TerminalKeyNames.From(e.Key, e.KeyModifiers);
        if (IsChord(name) && page.Terminal.Route(name) == TerminalKeyRoute.Handled)
        {
            e.Handled = true;
        }
    }

    // A press on the list while the pane has the keys gives them back to the list (spec §15.3: a click on the list always takes focus).
    private void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Page is { IsTerminalOpen: true } page && page.Terminal.Side != TerminalSide.List && e.Source is Visual source && !TerminalPanel.IsVisualAncestorOf(source))
        {
            page.Terminal.ClickList();
        }
    }

    private static bool IsChord(string name) =>
        string.Equals(name, "F6", StringComparison.Ordinal)
        || string.Equals(name, "Shift+F6", StringComparison.Ordinal)
        || string.Equals(name, "Ctrl+Shift+L", StringComparison.Ordinal)
        || string.Equals(name, "Cmd+Shift+L", StringComparison.Ordinal);
}
