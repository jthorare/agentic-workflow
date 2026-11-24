using Aw.Sindri.Ui.Terminal;

namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>Joins an open socket to the terminal view's state and the pane's drawing: frames in, keys out (agents spec §15.3, #3382).</summary>
// Continuations return to the caller's context (the UI thread), because each one changes the view model and draws.
internal sealed class TerminalSession(ITerminalConnector connector, TerminalViewViewModel view, Action<TerminalGrid> draw)
{
    private ITerminalLink? _link;
    private int _height;
    private int _hist;
    private bool _ready;
    private (int Cols, int Rows)? _sent;
    private (int Cols, int Rows)? _wanted;

    // The sizes the server accepts: a `size` outside them is refused. Mirrors TmuxTerminalPaneSource.Resize (src/Sindri/Aw.Sindri.Infrastructure/Launch/),
    // which the Ui must not reference. A fit outside them is clamped to the nearest accepted size, so tmux stays at the floor and the pane clips.
    private const int MinCols = 20;
    private const int MaxCols = 400;
    private const int MinRows = 5;
    private const int MaxRows = 200;

    /// <summary>The size the server would accept for a fit: each side clamped to 20..400 columns and 5..200 rows; null when a side is zero or less (no real fit).</summary>
    /// <param name="cols">Fitted columns.</param>
    /// <param name="rows">Fitted rows.</param>
    /// <returns>The size to send and to open with, or null.</returns>
    internal static (int Cols, int Rows)? Bounded(int cols, int rows) =>
        cols <= 0 || rows <= 0 ? null : (Math.Clamp(cols, MinCols, MaxCols), Math.Clamp(rows, MinRows, MaxRows));

    /// <summary>The rows above the live screen's top the pane shows or asked for (the socket's `scroll`, #3730); 0 is live. A frame sets it to the `back` it was drawn for.</summary>
    internal int Back { get; private set; }

    /// <summary>Connects and pumps until the socket ends or the token cancels; reports each state to the view.</summary>
    /// <param name="open">The session and the size the view fits.</param>
    /// <param name="cancellationToken">Ends the session (the view closed or switched).</param>
    /// <returns>When the socket has ended.</returns>
    internal async Task Run(TerminalOpen open, CancellationToken cancellationToken)
    {
        view.SocketOpening();
        (_height, _hist) = (0, 0);
        (_ready, _sent) = (false, null);   // the hello's size is not a resize (Serve resizes only on `size`), so the first fit goes out once after ready; a fit made before Run is kept in _wanted
        SetBack(0);
        if (await Open(open, cancellationToken).ConfigureAwait(true) is not { } link)
        {
            return;
        }

        _link = link;
        var broke = false;
        try
        {
            await Pump(link, cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return;
        }
#pragma warning disable CA1031 // why: a socket that dies without a close frame, or a frame that breaks the reader, must end in a drop (retry, 30 s rule, focus to the list), never leave the pane Attached
        catch (Exception)
        {
            broke = true;
        }
#pragma warning restore CA1031
        finally
        {
            _link = null;
            await link.DisposeAsync().ConfigureAwait(true);
        }

        Report(link, broke, cancellationToken);
    }

    // The socket ended: a drop (it broke) or a close by code. Nothing when the run was cancelled: the view switched or closed, and a socket that ended
    // meanwhile is not the shown pane's news (#3860).
    private void Report(ITerminalLink link, bool broke, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (broke)
        {
            view.SocketDropped();
            return;
        }

        view.SocketClosed(link.CloseCode ?? 1006);
    }

    // The connect: null when it was cancelled (nothing to report) or the socket could not open (a drop, reported here).
    private async Task<ITerminalLink?> Open(TerminalOpen open, CancellationToken cancellationToken)
    {
        ITerminalLink? link;
        try
        {
            link = await connector.Connect(open, cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return null;
        }

        if (link is null && !cancellationToken.IsCancellationRequested)
        {
            view.SocketDropped();
        }

        return link;
    }

    /// <summary>Sends a key the person typed in the pane; the view queues it until `ready` and drops it when the socket is down.</summary>
    /// <param name="name">The route name of the key.</param>
    /// <param name="text">The characters it typed, or null.</param>
    /// <param name="cancellationToken">Cancels the send.</param>
    /// <returns>What became of the key.</returns>
    internal async Task<TerminalKeyFate> SendKey(string name, string? text, CancellationToken cancellationToken) =>
        TerminalSocketMessages.FromKey(name, text) is { } message ? await Deliver(message, cancellationToken).ConfigureAwait(true) : TerminalKeyFate.Dropped;

    /// <summary>
    /// Shift+PageUp (pages 1) or Shift+PageDown (pages -1): one screen further back or towards live, as one `scroll` message. The offset is clamped to
    /// 0..the history the last frame named, so PageDown stops at live and PageUp past the oldest row sends nothing.
    /// </summary>
    /// <param name="pages">Screens to move: positive back, negative towards live.</param>
    /// <param name="cancellationToken">Cancels the send.</param>
    /// <returns>True when a scroll message went out; false when the offset would not change or the socket is down.</returns>
    internal async Task<bool> ScrollPage(int pages, CancellationToken cancellationToken)
    {
        var back = Math.Clamp(Back + (pages * _height), 0, _hist);   // the socket table: the client maps Shift+PageUp to back + h
        if (back == Back || _link is not { } link)
        {
            return false;
        }

        SetBack(back);
        return await link.Send(TerminalSocketMessages.Scroll(back), cancellationToken).ConfigureAwait(true);
    }

    // One message out: queued until ready, dropped when the socket is down. A key, text or paste that goes out puts the pane back on live: Serve
    // sets its offset to 0 on the same message, so no scroll message is sent.
    private async Task<TerminalKeyFate> Deliver(string message, CancellationToken cancellationToken)
    {
        var fate = view.SendKey(message);
        if (fate is TerminalKeyFate.Sent or TerminalKeyFate.Queued)
        {
            SetBack(0);
        }

        if (fate == TerminalKeyFate.Sent && _link is { } link)
        {
            return await link.Send(message, cancellationToken).ConfigureAwait(true) ? TerminalKeyFate.Sent : TerminalKeyFate.Dropped;
        }

        return fate == TerminalKeyFate.Sent ? TerminalKeyFate.Dropped : fate;
    }

    /// <summary>Sends pasted text as one paste message (spec 15.8: literal text, at most 256 KiB); nothing for an empty paste.</summary>
    /// <param name="text">The clipboard's text.</param>
    /// <param name="cancellationToken">Cancels the send.</param>
    /// <returns>What became of the paste.</returns>
    internal Task<TerminalKeyFate> SendPaste(string text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Task.FromResult(TerminalKeyFate.Dropped);
        }

        if (System.Text.Encoding.UTF8.GetByteCount(text) > TerminalSocketMessages.MaxPasteBytes)
        {
            return Task.FromResult(TerminalKeyFate.TooBig);   // Serve would close the socket with 1009
        }

        // The writer escapes < > & ' + and non-ASCII as six bytes each, and Serve closes the socket over a message above its own cap before it decodes it.
        var message = TerminalSocketMessages.Paste(text);
        if (System.Text.Encoding.UTF8.GetByteCount(message) > TerminalSocketMessages.MaxMessageBytes)
        {
            return Task.FromResult(TerminalKeyFate.TooBig);
        }

        return Deliver(message, cancellationToken);
    }

    /// <summary>The size the pane fits: sent as a `size` message once the socket is ready (#3860).</summary>
    /// <param name="cols">Columns.</param>
    /// <param name="rows">Rows.</param>
    /// <param name="cancellationToken">Cancels the send.</param>
    /// <returns>True when a size message went out.</returns>
    internal async Task<bool> FitSize(int cols, int rows, CancellationToken cancellationToken)
    {
        if (Bounded(cols, rows) is not { } size)
        {
            return false;
        }

        _wanted = size;
        return _ready && _link is { } link && await SendWanted(link, cancellationToken).ConfigureAwait(true);
    }

    // The wanted size, unless the socket already has it or a send of it is in flight: one `size` message.
    private async Task<bool> SendWanted(ITerminalLink link, CancellationToken cancellationToken)
    {
        if (_wanted is not { } size || size == _sent)
        {
            return false;
        }

        // Taken before the await: a fit of the same size that lands while this send is in flight finds it taken and sends nothing.
        var before = _sent;
        _sent = size;
        if (!await link.Send(TerminalSocketMessages.Size(size.Cols, size.Rows), cancellationToken).ConfigureAwait(true))
        {
            if (_sent == size)
            {
                _sent = before;   // the send failed and no later fit replaced it: the socket does not have this size
            }

            return false;
        }

        return true;
    }

    /// <summary>Ctrl-C through the socket.</summary>
    /// <param name="cancellationToken">Cancels the send.</param>
    /// <returns>True when the key was sent.</returns>
    internal async Task<bool> Interrupt(CancellationToken cancellationToken)
    {
        if (_link is not { } link)
        {
            return false;
        }

        SetBack(0);   // a key: Serve goes back to live
        return await link.Send(TerminalSocketMessages.Interrupt(), cancellationToken).ConfigureAwait(true);
    }

    // The offset the pane shows; the view's "Scrolled back" line follows it.
    private void SetBack(int back)
    {
        Back = back;
        view.ScrolledBack(back);
    }

    private async Task Pump(ITerminalLink link, CancellationToken cancellationToken)
    {
        await foreach (var message in link.Read(cancellationToken).ConfigureAwait(true))
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (message.Kind)
            {
                case TerminalServerKind.Ready:
                    foreach (var queued in view.Ready())
                    {
                        _ = await link.Send(queued, cancellationToken).ConfigureAwait(true);
                    }

                    _ready = true;
                    _ = await SendWanted(link, cancellationToken).ConfigureAwait(true);
                    break;
                case TerminalServerKind.Frame:
                    (_height, _hist) = (message.Status.Height ?? message.Rows.Count, message.Status.Hist);
                    SetBack(message.Status.Back);   // the frame's own offset: one drawn for an older scroll may land after a key
                    view.FrameReceived(message.Rows, message.Status.Dead);
                    draw(TerminalFrameParser.Parse(message.Rows, message.Status));
                    break;
                case TerminalServerKind.Ping:
                    _ = await link.Send(TerminalSocketMessages.Pong(), cancellationToken).ConfigureAwait(true);
                    break;
                case TerminalServerKind.Unknown:
                default:
                    break;
            }
        }
    }
}
