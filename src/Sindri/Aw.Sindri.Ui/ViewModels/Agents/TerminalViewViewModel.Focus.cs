#pragma warning disable MA0182 // see TerminalViewViewModel.cs
namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>The terminal view (continued): focus rules, key routing, footers and borders (agents spec §15.3 "Focus", "Where keys go").</summary>
internal sealed partial class TerminalViewViewModel
{
    private bool _modalOpen;

    /// <summary>The side that has keyboard focus.</summary>
    public TerminalSide Side { get; private set; } = TerminalSide.List;

    public bool IsPaneAccented => Side == TerminalSide.Pane;

    public bool IsListAccented => Side == TerminalSide.List;

    /// <summary>True while a card button has focus.</summary>
    public bool IsCardAccented => Side == TerminalSide.Card;

    /// <summary>True while the pane lacks focus: its cursor is a hollow block.</summary>
    public bool IsCursorHollow => Side != TerminalSide.Pane;

    /// <summary>False while the pane has focus: page and shell keys, the G chord included, are off.</summary>
    public bool ShellKeysEnabled => Side == TerminalSide.List;

    /// <summary>False while the pane has focus: Ctrl+K goes to the agent.</summary>
    public bool PaletteEnabled => Side == TerminalSide.List;

    /// <summary>The footer for the focused side.</summary>
    public string FooterText => Side switch
    {
        TerminalSide.Pane => Labels.TerminalFooterPane(HeaderName, IsMac),
        TerminalSide.Card => Labels.TerminalFooterCard(ButtonLabel(CardButtons[FocusedButton])),
        TerminalSide.List => ListFooter(),
        _ => string.Empty,
    };

    /// <summary>The footer split into parts and key names, drawn with keycaps (DG2).</summary>
    public IReadOnlyList<TerminalFooterPart> FooterParts => TerminalFooterPart.Parse(FooterText);

    /// <summary>Opens the view on a session.</summary>
    /// <param name="target">The session.</param>
    /// <param name="intent">Look (Enter) or Type (r, Ctrl+Enter on a permission or login row).</param>
    public void Open(TerminalTarget target, TerminalOpenIntent intent)
    {
        ArgumentNullException.ThrowIfNull(target);
        Begin(target);
        Side = target.CanAttach && (intent == TerminalOpenIntent.Type || target.Kind == TerminalKind.OneOff) ? TerminalSide.Pane : TerminalSide.List;
        Refresh();
    }

    /// <summary>Enter on the row the pane already shows.</summary>
    public void PressEnterOnShownRow() => FocusRight();

    /// <summary>A click in the pane, or on the card.</summary>
    public void ClickPane() => FocusRight();

    /// <summary>A click on the list: it always takes focus.</summary>
    public void ClickList()
    {
        Side = TerminalSide.List;
        Refresh();
    }

    /// <summary>Routes one key: "F6", "Shift+F6", the second chord, "Esc", or any other key name.</summary>
    /// <param name="key">The key.</param>
    /// <returns>Who gets the key.</returns>
    public TerminalKeyRoute Route(string key)
    {
        if (_modalOpen)
        {
            return TerminalKeyRoute.Page;
        }

        if (IsChord(key))
        {
            SwapSide();
            return TerminalKeyRoute.Handled;
        }

        if (Side == TerminalSide.Pane)
        {
            return TerminalKeyRoute.Pane;
        }

        if (Side == TerminalSide.Card)
        {
            return RouteOnCard(key);
        }

        if (string.Equals(key, "Esc", StringComparison.Ordinal))
        {
            Raise(CloseRequested);
            return TerminalKeyRoute.Handled;
        }

        return TerminalKeyRoute.Page;
    }

    /// <summary>A modal (the `.` menu, the stop confirm) opened over the view: it takes the keys.</summary>
    public void OpenModal() => _modalOpen = true;

    /// <summary>The modal closed: focus is where it was.</summary>
    public void CloseModal() => _modalOpen = false;

    /// <summary>Close in the header: the session keeps running.</summary>
    public void Close() => Raise(CloseRequested);

    private bool IsChord(string key) =>
        string.Equals(key, "F6", StringComparison.Ordinal)
        || string.Equals(key, "Shift+F6", StringComparison.Ordinal)
        || string.Equals(key, IsMac ? "Cmd+Shift+L" : "Ctrl+Shift+L", StringComparison.Ordinal);

    private void SwapSide()
    {
        if (Side != TerminalSide.List)
        {
            Side = TerminalSide.List;
            Refresh();
            return;
        }

        FocusRight();
    }

    // The right side takes focus: the pane when it is live, the card's first button in a card state, nothing while Reconnecting or Connecting.
    private void FocusRight()
    {
        if (CardButtons.Count > 0)
        {
            (Side, FocusedButton) = (TerminalSide.Card, 0);
            Refresh();
            return;
        }

        FocusPane();
    }

    private string ListFooter() =>
        CardButtons.Count > 0 ? Labels.TerminalFooterListCard(ButtonLabel(CardButtons[0]))
        : State == TerminalState.Reconnecting ? Labels.TerminalFooterReconnecting
        : State is TerminalState.CantAttach or TerminalState.Ended or TerminalState.Gone ? Labels.TerminalFooterListOnly
        : Labels.TerminalFooterList(HeaderName, IsMac);

    private TerminalKeyRoute RouteOnCard(string key)
    {
        if (string.Equals(key, "Esc", StringComparison.Ordinal))
        {
            ClickList();
        }
        else if (string.Equals(key, "Tab", StringComparison.Ordinal) || string.Equals(key, "Shift+Tab", StringComparison.Ordinal))
        {
            var step = string.Equals(key, "Tab", StringComparison.Ordinal) ? 1 : -1;
            FocusedButton = (FocusedButton + step + CardButtons.Count) % CardButtons.Count;
            Refresh();
        }
        else if (string.Equals(key, "Enter", StringComparison.Ordinal))
        {
            Press(CardButtons[FocusedButton]);
        }
        else
        {
            return TerminalKeyRoute.Page;
        }

        return TerminalKeyRoute.Handled;
    }

    private void FocusPane()
    {
        if (!CanTakeFocus)
        {
            return;
        }

        Side = TerminalSide.Pane;
        Refresh();
    }
}
