using Aw.Sindri.Ui.Terminal;

namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>The Agents page (continued): the terminal view beside the list (agents spec §15.1, §15.3, U7a). Enter opens it with focus in the list; Esc or Close closes it; j/k or a click switches it.</summary>
internal sealed partial class AgentsPageViewModel
{
    private bool _terminalWired;
    private TerminalSession? _session;
    private CancellationTokenSource? _sessionEnds;
    private (int Cols, int Rows)? _fit;

    // What a socket opens with until the pane has fitted its own size.
    private const int DefaultCols = 120;
    private const int DefaultRows = 36;

    /// <summary>Draws a parsed frame into the pane; the view sets it.</summary>
    internal Action<TerminalGrid>? DrawFrame { get; set; }

    /// <summary>A key the person typed in the pane goes to the session's socket.</summary>
    /// <param name="name">The route name.</param>
    /// <param name="text">The characters it typed, or null.</param>
    /// <returns>What became of the key.</returns>
    internal Task<TerminalKeyFate> SendPaneKey(string name, string? text) => _session?.SendKey(name, text, CancellationToken.None) ?? Task.FromResult(TerminalKeyFate.Dropped);

    /// <summary>Text pasted into the pane goes to the session's socket as one paste.</summary>
    /// <param name="text">The clipboard's text.</param>
    /// <returns>What became of the paste.</returns>
    internal async Task<TerminalKeyFate> SendPanePaste(string text)
    {
        var fate = _session is null ? TerminalKeyFate.Dropped : await _session.SendPaste(text, CancellationToken.None).ConfigureAwait(true);
        Terminal.NoteFate(fate);
        return fate;
    }

    /// <summary>Shift+PageUp or Shift+PageDown in the pane: one screen of tmux history further back or towards live, through the session's socket (#3730).</summary>
    /// <param name="pages">Screens to move: positive back, negative towards live.</param>
    /// <returns>True when a scroll message went out.</returns>
    internal Task<bool> ScrollPane(int pages) => _session?.ScrollPage(pages, CancellationToken.None) ?? Task.FromResult(false);

    /// <summary>The pane fitted a new size (columns by rows): the open socket gets it as a `size` message, and a later socket opens with it (#3860).</summary>
    /// <param name="cols">Columns.</param>
    /// <param name="rows">Rows.</param>
    /// <returns>True when a size message went out.</returns>
    internal Task<bool> FitPane(int cols, int rows)
    {
        if (TerminalSession.Bounded(cols, rows) is not { } size)
        {
            return Task.FromResult(false);   // no real fit (zero or less): keep the last one
        }

        _fit = size;   // clamped to what the server accepts, so the next hello never opens with a size it would refuse
        return _session?.FitSize(size.Cols, size.Rows, CancellationToken.None) ?? Task.FromResult(false);
    }

    /// <summary>The terminal view's state.</summary>
    internal TerminalViewViewModel Terminal => field ??= NewTerminal();

    /// <summary>True while the terminal view is open beside the list.</summary>
    public bool IsTerminalOpen { get; private set => Set(ref field, value); }

    /// <summary>The key of the row the pane shows; null when closed.</summary>
    public string? TerminalKey { get; private set => Set(ref field, value); }

    /// <summary>Opens the terminal view on a row, or moves focus into the pane when it already shows that row.</summary>
    /// <param name="row">The row.</param>
    /// <param name="intent">Look: focus starts in the list; Type: in the pane.</param>
    internal void OpenTerminal(SessionRowViewModel row, TerminalOpenIntent intent = TerminalOpenIntent.Look)
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
            Terminal.Open(target, intent);
        }

        (TerminalKey, IsTerminalOpen) = (row.Ref, true);
        StartSession(row);
    }

    /// <summary>Opens the pane on the row with this key and puts focus in the pane (the cursor in the agent's prompt): the shell's AgentPaneRequested (New agent form, #3384) calls it after the new row is drawn.</summary>
    /// <param name="key">The row's key (a ref, or "sindri-&lt;actId&gt;" for a one-off).</param>
    /// <returns>True when such a row is drawn.</returns>
    internal bool OpenTerminalByKey(string key)
    {
        if (Walk().Find(w => string.Equals(w.Key, key, StringComparison.Ordinal)).Live is not { } row || !Select(key))
        {
            return false;
        }

        OpenTerminal(row, TerminalOpenIntent.Type);
        return true;
    }

    /// <summary>Closes the view: the list returns to full width on the same row and the session keeps running.</summary>
    internal void CloseTerminal()
    {
        if (!IsTerminalOpen)
        {
            return;
        }

        (TerminalKey, IsTerminalOpen) = (null, false);
        StopSession();
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
            StartSession(row);
        }
    }

    private TerminalViewViewModel NewTerminal()
    {
        var paneControl = control ?? (stopper is null ? null : new PaneControl(stopper, () => Url, token => _session?.Interrupt(token) ?? Task.FromResult(false)));
        var view = new TerminalViewViewModel(_clock, OperatingSystem.IsMacOS(), paneControl);
        if (!_terminalWired)
        {
            _terminalWired = true;
            view.CloseRequested += (_, _) => CloseTerminal();
            view.SocketRequested += (_, _) => Reconnect();
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

    // Opens the row's terminal socket (and drops the last one): frames draw into the pane, keys go out through it.
    private void StartSession(SessionRowViewModel row)
    {
        StopSession();
        if (connector is null || Url is not { } api || row.Row.Host.Pane is not { Length: > 0 })
        {
            return;
        }

#pragma warning disable CA2000 // the field keeps it, and StopSession cancels and disposes it when the pane closes or switches
        var ends = new CancellationTokenSource();
#pragma warning restore CA2000
        var session = new TerminalSession(connector, Terminal, grid => DrawFrame?.Invoke(grid));
        (_sessionEnds, _session) = (ends, session);
        if (_fit is { } fit)
        {
            _ = session.FitSize(fit.Cols, fit.Rows, CancellationToken.None);   // the pane fitted before this socket opened: Serve ignores the hello's size, so it goes out after ready
        }

        _ = Pump(session, new TerminalOpen(api, row.Row.Provider, row.Row.Id, _fit?.Cols ?? DefaultCols, _fit?.Rows ?? DefaultRows), ends.Token);
    }

    private static async Task Pump(TerminalSession session, TerminalOpen open, CancellationToken token)
    {
        try
        {
            await session.Run(open, token).ConfigureAwait(true);
        }
        catch (Exception e) when (e is IOException or InvalidOperationException or System.Net.WebSockets.WebSocketException or HttpRequestException)
        {
            // a socket that broke is a drop: the view model already reports it on the next retry
        }
    }

    private void StopSession()
    {
        _sessionEnds?.Cancel();
        _sessionEnds?.Dispose();
        (_sessionEnds, _session) = (null, null);
    }

    // The retry came due (or Retry now / Take it back): open the current row's socket again.
    private void Reconnect()
    {
        if (TerminalKey is { } key && Walk().Find(w => string.Equals(w.Key, key, StringComparison.Ordinal)).Live is { } row)
        {
            StartSession(row);
        }
    }
}
