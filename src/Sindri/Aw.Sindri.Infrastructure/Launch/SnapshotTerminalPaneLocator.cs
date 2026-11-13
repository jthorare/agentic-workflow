using System.Text.RegularExpressions;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>
/// Finds the pane of a session from the collector's last pass (agents spec §15.2). A 'sindri-&lt;actId&gt;' name is a one-off session on the launch socket
/// (the session name stands for its one pane until the agent's fleet file joins it by session name plus pane id). Any other id is a discovered
/// session: its tmux facts give the socket path and the pane id; no tmux facts is SND-AGENT-015 with the host kind, no such session is 016.
/// </summary>
internal sealed partial class SnapshotTerminalPaneLocator(ISessionSnapshot snapshot, TmuxHostOptions options) : ITerminalPaneLocator
{
    /// <inheritdoc/>
    public ValueTask<Result<TerminalTarget>> Find(SessionRef session, CancellationToken cancellationToken)
    {
        var id = session.Id.Value;
        if (OneOff.IsMatch(id))
        {
            return ValueTask.FromResult<Result<TerminalTarget>>(new TerminalTarget(options.Socket, SocketIsPath: false, id, id, Foreign: false));
        }

        if (snapshot.Current is not { } pass || SessionRows.Find(pass, session) is not { } found)
        {
            return ValueTask.FromResult<Result<TerminalTarget>>(SindriErrors.AgentPaneGone.With("no live session has that ref"));
        }

        if (found.Host.Tmux is not { Socket: { Length: > 0 } socket, Session: { Length: > 0 } name, Pane: { Length: > 0 } pane })
        {
            return ValueTask.FromResult<Result<TerminalTarget>>(SindriErrors.AgentNoPane.With($"the session runs in {found.Host.Label} ({found.Host.Kind}), not in a tmux pane"));
        }

        var own = string.Equals(found.Host.Tmux.SocketKind, "sindri", StringComparison.Ordinal);
        return ValueTask.FromResult<Result<TerminalTarget>>(new TerminalTarget(socket, SocketIsPath: true, name, pane, Foreign: !own));
    }

    [GeneratedRegex(@"\Asindri-[a-z0-9]{8,32}\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex OneOff { get; }
}
