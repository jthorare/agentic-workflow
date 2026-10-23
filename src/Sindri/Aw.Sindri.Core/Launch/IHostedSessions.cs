using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Launch;

/// <summary>
/// A session host whose sessions run without 'session wrap' (T3): it reports which of its sessions are still alive, so the silence sweep keeps them live and
/// ends the ones that are gone, where a wrap's heartbeat and exit would do it.
/// </summary>
public interface IHostedSessions
{
    /// <summary>Looks at every session this host started and hasn't forgotten.</summary>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>One state per session.</returns>
    public ValueTask<IReadOnlyList<HostedSession>> Check(CancellationToken cancellationToken);

    /// <summary>Stops reporting a launch's session (its end is recorded). Safe to repeat.</summary>
    /// <param name="launch">The launch.</param>
    public void Forget(LaunchId launch);
}
