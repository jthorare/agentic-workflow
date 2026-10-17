using Aw.Kernel;

namespace Aw.Sindri.Core.Launch;

/// <summary>
/// What owns a launched agent process (launcher spec SL4; port; the tmux adapter runs 'tmux -L sindri-launch'). Every member is
/// safe to repeat: a session that is already gone is not an error.
/// </summary>
public interface ISessionHost
{
    /// <summary>What this host is.</summary>
    public HostKind Kind { get; }

    /// <summary>Starts <paramref name="session"/> and returns once it exists; the adapter removes a session whose start overran <paramref name="timeout"/>.</summary>
    /// <param name="session">The session to run.</param>
    /// <param name="timeout">The start deadline (4 s).</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The started session, or an error: a session of that name already exists (conflict), or the host failed or overran (SND-LAUNCH-009).</returns>
    public ValueTask<Result<HostRef>> Start(HostSession session, TimeSpan timeout, CancellationToken cancellationToken);

    /// <summary>Ends the session. Idempotent: a session that is gone, or never existed, is a success.</summary>
    /// <param name="host">The session.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success, or an error when the host could not be asked.</returns>
    public ValueTask<Result<Unit>> Stop(HostRef host, CancellationToken cancellationToken);

    /// <summary>The number of clients attached to the session now.</summary>
    /// <param name="host">The session.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The count; 0 for a session that is gone.</returns>
    public ValueTask<int> AttachedClients(HostRef host, CancellationToken cancellationToken);

    /// <summary>The command that attaches a terminal to the session.</summary>
    /// <param name="host">The session.</param>
    /// <returns>The argv, built from the session's checked name.</returns>
    public IReadOnlyList<string> AttachArgv(HostRef host);

    /// <summary>Whether this host can start sessions here.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The facts the probe records.</returns>
    public ValueTask<HostFacts> Probe(CancellationToken cancellationToken);
}
