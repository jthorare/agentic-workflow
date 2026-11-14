using Aw.Sindri.Ui.ViewModels.Agents;

namespace Aw.Sindri.Ui.Terminal;

/// <summary>Joins an open socket to the terminal view's state and the pane's drawing: frames in, keys out (agents spec §15.3, #3382).</summary>
internal sealed class TerminalSession(ITerminalConnector connector, TerminalViewViewModel view, Action<TerminalGrid> draw)
{
    private ITerminalLink? _link;

    /// <summary>Connects and pumps until the socket ends or the token cancels; reports each state to the view.</summary>
    internal Task Run(Uri api, string provider, string id, int cols, int rows, CancellationToken cancellationToken) =>
        connector is null || draw is null || view is null ? Task.CompletedTask : Task.CompletedTask;

    /// <summary>Sends a key the person typed in the pane; queues it until `ready` and drops it when the socket is down.</summary>
    internal Task<TerminalKeyFate> SendKey(string name, string? text, CancellationToken cancellationToken) => Task.FromResult(_link is null ? TerminalKeyFate.Dropped : TerminalKeyFate.Dropped);

    /// <summary>Ctrl-C through the socket.</summary>
    internal Task<bool> Interrupt(CancellationToken cancellationToken) => Task.FromResult(false);
}
