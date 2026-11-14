using Aw.Sindri.Ui.ViewModels.Agents;

namespace Aw.Sindri.Ui.Terminal;

/// <summary>Joins an open socket to the terminal view's state and the pane's drawing: frames in, keys out (agents spec §15.3, #3382).</summary>
// Continuations return to the caller's context (the UI thread), because each one changes the view model and draws.
internal sealed class TerminalSession(ITerminalConnector connector, TerminalViewViewModel view, Action<TerminalGrid> draw)
{
    private ITerminalLink? _link;

    /// <summary>Connects and pumps until the socket ends or the token cancels; reports each state to the view.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="provider">The session's provider.</param>
    /// <param name="id">The session's id.</param>
    /// <param name="cols">The columns the view fits.</param>
    /// <param name="rows">The rows the view fits.</param>
    /// <param name="cancellationToken">Ends the session (the view closed or switched).</param>
    /// <returns>When the socket has ended.</returns>
    internal async Task Run(Uri api, string provider, string id, int cols, int rows, CancellationToken cancellationToken)
    {
        view.SocketOpening();
        ITerminalLink? link;
        try
        {
            link = await connector.Connect(api, provider, id, cols, rows, cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (link is null)
        {
            view.SocketDropped();
            return;
        }

        _link = link;
        try
        {
            await Pump(link, cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            _link = null;
            await link.DisposeAsync().ConfigureAwait(true);
        }

        view.SocketClosed(link.CloseCode ?? 1006);
    }

    /// <summary>Sends a key the person typed in the pane; the view queues it until `ready` and drops it when the socket is down.</summary>
    /// <param name="name">The route name of the key.</param>
    /// <param name="text">The characters it typed, or null.</param>
    /// <param name="cancellationToken">Cancels the send.</param>
    /// <returns>What became of the key.</returns>
    internal async Task<TerminalKeyFate> SendKey(string name, string? text, CancellationToken cancellationToken)
    {
        return TerminalSocketMessages.FromKey(name, text) is { } message ? await Deliver(message, cancellationToken).ConfigureAwait(true) : TerminalKeyFate.Dropped;
    }

    // One message out: queued until ready, dropped when the socket is down.
    private async Task<TerminalKeyFate> Deliver(string message, CancellationToken cancellationToken)
    {
        var fate = view.SendKey(message);
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
    internal Task<TerminalKeyFate> SendPaste(string text, CancellationToken cancellationToken) =>
        string.IsNullOrEmpty(text) ? Task.FromResult(TerminalKeyFate.Dropped) : Deliver(TerminalSocketMessages.Paste(text), cancellationToken);

    /// <summary>Ctrl-C through the socket.</summary>
    /// <param name="cancellationToken">Cancels the send.</param>
    /// <returns>True when the key was sent.</returns>
    internal async Task<bool> Interrupt(CancellationToken cancellationToken) =>
        _link is { } link && await link.Send(TerminalSocketMessages.Interrupt(), cancellationToken).ConfigureAwait(true);

    private async Task Pump(ITerminalLink link, CancellationToken cancellationToken)
    {
        await foreach (var message in link.Read(cancellationToken).ConfigureAwait(true))
        {
            switch (message.Kind)
            {
                case TerminalServerKind.Ready:
                    foreach (var queued in view.Ready())
                    {
                        _ = await link.Send(queued, cancellationToken).ConfigureAwait(true);
                    }

                    break;
                case TerminalServerKind.Frame:
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
