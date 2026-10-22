using Aw.Kernel;

namespace Aw.Sindri.Core.Forge;

/// <summary>
/// The pull requests the 'pr-watch' poller follows (PR babysitter spec §6.2): the 'fact:pr' of every live instance that waits on 'pr.changed'. A narrow read; it
/// starts and signals nothing. The adapter reads the instances' states and facts, so an instance that ended, or waits on anything else, is not listed.
/// </summary>
public interface IWatchedPulls
{
    /// <summary>Lists the watched pull requests.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>One row per (link, item), in a stable order; empty when nothing waits; or the error when the instances can't be read.</returns>
    public ValueTask<Result<IReadOnlyList<WatchedPull>>> List(CancellationToken cancellationToken);
}
