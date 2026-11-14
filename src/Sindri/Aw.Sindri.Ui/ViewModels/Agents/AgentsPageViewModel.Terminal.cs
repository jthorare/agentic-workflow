namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>The Agents page (continued): the terminal view beside the list (agents spec §15.1, §15.3, U7a). Enter opens it with focus in the list; Esc or Close closes it; j/k or a click switches it.</summary>
internal sealed partial class AgentsPageViewModel
{
    private bool _terminalWired;

    /// <summary>The terminal view's state.</summary>
    internal TerminalViewViewModel Terminal => field ??= NewTerminal();

    /// <summary>True while the terminal view is open beside the list.</summary>
    public bool IsTerminalOpen { get; private set => Set(ref field, value); }

    /// <summary>The key of the row the pane shows; null when closed.</summary>
    public string? TerminalKey { get; private set => Set(ref field, value); }

    /// <summary>Opens the terminal view on a row, or moves focus into the pane when it already shows that row.</summary>
    /// <param name="row">The row.</param>
    internal void OpenTerminal(SessionRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (IsTerminalOpen && string.Equals(TerminalKey, row.Ref, StringComparison.Ordinal))
        {
            Terminal.PressEnterOnShownRow();
            return;
        }

        var target = TargetOf(row);
        if (IsTerminalOpen)
        {
            Terminal.SwitchTo(target);
        }
        else
        {
            Terminal.Open(target, TerminalOpenIntent.Look);
        }

        (TerminalKey, IsTerminalOpen) = (row.Ref, true);
    }

    /// <summary>Closes the view: the list returns to full width on the same row and the session keeps running.</summary>
    internal void CloseTerminal()
    {
        if (!IsTerminalOpen)
        {
            return;
        }

        (TerminalKey, IsTerminalOpen) = (null, false);
        if (SelectedKey is { } key)
        {
            RequestFocus(new AgentFocusEventArgs(key, toFocusLine: false, onlyIfLost: false));
        }
    }

    /// <summary>The Transcript button: opens the §6.3 sheet on the row the pane shows.</summary>
    internal void OpenTranscript()
    {
        if (TerminalKey is { } key && Walk().Find(w => string.Equals(w.Key, key, StringComparison.Ordinal)).Live is { } row)
        {
            RequestOpen(new SessionOpenRequestedEventArgs(row.Link));
        }
    }

    // The selection moved while the view is open: the pane follows, and focus stays in the list.
    private void FollowSelection()
    {
        if (IsTerminalOpen && SelectedLive is { } row && !string.Equals(TerminalKey, row.Ref, StringComparison.Ordinal))
        {
            Terminal.SwitchTo(TargetOf(row));
            TerminalKey = row.Ref;
        }
    }

    private TerminalViewViewModel NewTerminal()
    {
        var view = new TerminalViewViewModel(_clock, OperatingSystem.IsMacOS(), control);
        if (!_terminalWired)
        {
            _terminalWired = true;
            view.CloseRequested += (_, _) => CloseTerminal();
        }

        return view;
    }

    private static TerminalTarget TargetOf(SessionRowViewModel row)
    {
        var host = row.Row.Host;
        var canAttach = host.Pane is { Length: > 0 };
        return new TerminalTarget
        {
            Key = row.Ref,
            Name = row.LaneText.Length > 0 ? row.LaneText : row.TitleText,
            Kind = row.Row.Lane is null ? TerminalKind.Listed : TerminalKind.Lane,
            StartedBySindri = row.IsLaunched,
            SocketKind = host.SocketKind ?? string.Empty,
            CanAttach = canAttach,
            CantAttachReason = canAttach ? string.Empty : host.Label + ": " + Labels.TerminalNoPane,
        };
    }
}
