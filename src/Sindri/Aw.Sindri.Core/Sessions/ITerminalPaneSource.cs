using Aw.Kernel;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Sessions;

/// <summary>
/// Serve's side of the pane seam (agents spec §15.7, way C): it produces the frames of one pane and takes keys, paste and size in.
/// The adapter polls 'tmux capture-pane' and types with 'send-keys', both through <see cref="TmuxAllowlist"/>. It never logs a frame or a key.
/// </summary>
public interface ITerminalPaneSource
{
    /// <summary>Opens a stream on the pane of a session.</summary>
    /// <param name="session">The session: its provider ('claude-code') and its provider session id, or the tmux session name 'sindri-&lt;actId&gt;'.</param>
    /// <param name="size">The size to draw at, or null for the pane's own.</param>
    /// <param name="cancellationToken">Cancels the open.</param>
    /// <returns>The stream, SND-AGENT-015 when the session has no tmux pane, or SND-AGENT-016 when the tmux server or session is gone.</returns>
    public ValueTask<Result<IOpenTerminalPane>> Open(SessionRef session, HostSize? size, CancellationToken cancellationToken);
}
