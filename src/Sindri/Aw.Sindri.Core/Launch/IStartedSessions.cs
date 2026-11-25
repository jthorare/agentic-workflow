using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Launch;

/// <summary>
/// The one-off sessions this process started, by act id (agents spec §15.8): the stored answer of a 2xx start and the worktree it made. The 'agent.op' event keyed by
/// <see cref="Events.AgentOpRecorded.KeyFor"/> is the durable copy; a miss after a restart reads it back once. The start and the worktree remove share it, so it is a port here and not a slice type.
/// </summary>
public interface IStartedSessions
{
    /// <summary>Holds the act id's gate: two calls with one act id run one after the other, so the second sees the first's result. Dispose releases it.</summary>
    /// <param name="actId">The act id.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The held gate.</returns>
    public ValueTask<IDisposable> Enter(ActId actId, CancellationToken cancellationToken);

    /// <summary>The 2xx start stored under <paramref name="actId"/> in this process, or null.</summary>
    /// <param name="actId">The act id.</param>
    /// <returns>The entry, or null.</returns>
    public StartedEntry? InMemory(ActId actId);

    /// <summary>Stores the 2xx start under the act id.</summary>
    /// <param name="actId">The act id.</param>
    /// <param name="entry">What the start left behind.</param>
    public void Keep(ActId actId, StartedEntry entry);

    /// <summary>The start stored under <paramref name="actId"/>: this process's, else the ledger's 'agent.op' event.</summary>
    /// <param name="actId">The act id.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The entry, or null.</returns>
    public ValueTask<StartedEntry?> Find(ActId actId, CancellationToken cancellationToken);
}
