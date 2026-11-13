using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Launch;

/// <summary>Where a started tmux session's pane is (agents spec §15.8): the socket and the pane id that every later call targets.</summary>
public interface ISessionPanes
{
    /// <summary>The tmux socket name the sessions live on ('sindri-launch').</summary>
    public string Socket { get; }

    /// <summary>The pane id of the session's first pane.</summary>
    /// <param name="host">The session.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The pane id ('%N'), or null when the session is gone or tmux could not be asked.</returns>
    public ValueTask<TmuxPaneId?> PaneId(HostRef host, CancellationToken cancellationToken);

    /// <summary>Whether the session's agent still runs (agents spec §15.8: a worktree is removed only once its session has Ended).</summary>
    /// <param name="host">The session.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>False when the session is gone or its first pane is dead, true while it runs, null when tmux could not be asked.</returns>
    public ValueTask<bool?> Running(HostRef host, CancellationToken cancellationToken);
}
