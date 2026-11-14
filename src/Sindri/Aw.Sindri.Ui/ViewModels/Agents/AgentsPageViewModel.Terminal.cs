using Aw.Sindri.Ui.Terminal;

namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>The Agents page (continued): the terminal view beside the list (agents spec §15.1, §15.3, U7a). Enter opens it with focus in the list; Esc or Close closes it; j/k or a click switches it.</summary>
internal sealed partial class AgentsPageViewModel
{
    private bool _terminalWired;
    private TerminalSession? _session;
    private CancellationTokenSource? _sessionEnds;

    /// <summary>Draws a parsed frame into the pane; the view sets it.</summary>
    internal Action<TerminalGrid>? DrawFrame { get; set; }

    /// <summary>A key the person typed in the pane goes to the session's socket.</summary>
    /// <param name="name">The route name.</param>
    /// <param name="text">The characters it typed, or null.</param>
    /// <returns>What became of the key.</returns>
    internal Task<TerminalKeyFate> SendPaneKey(string name, string? text) => _session?.SendKey(name, text, CancellationToken.None) ?? Task.FromResult(TerminalKeyFate.Dropped);

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
        StartSession(row);
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

        var ends = new CancellationTokenSource();
        var session = new TerminalSession(connector, Terminal, grid => DrawFrame?.Invoke(grid));
        (_sessionEnds, _session) = (ends, session);
        _ = Pump(session, api, row.Row.Provider, row.Row.Id, ends.Token);
    }

    private static async Task Pump(TerminalSession session, Uri api, string provider, string id, CancellationToken token)
    {
        try
        {
            await session.Run(api, provider, id, 120, 36, token).ConfigureAwait(true);
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
