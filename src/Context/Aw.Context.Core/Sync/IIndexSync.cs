using Aw.Context.Core.Engine;
using Aw.Context.Core.Results;
using Aw.Kernel;

namespace Aw.Context.Core.Sync;

/// <summary>
/// The record feeds of the owned index as one port for Sindri's 'index-sync' service (owned-index spec §5.5, §5.8): run one tick of every feed, and read where each source stands.
/// Neither touches the intent sources or the repo docs.
/// </summary>
public interface IIndexSync
{
    /// <summary>Runs one tick of every record feed of the profile, one after the other; a feed that is down is its row's code, not an error.</summary>
    /// <param name="scope">The profile's settings and repos, and the caller.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>One row per feed, in source order (none without a feed), or the error the scope or settings give.</returns>
    public ValueTask<Result<SyncReport>> Tick(ContextScope scope, CancellationToken cancellationToken);

    /// <summary>Reads each feed's phase, last tick and milestones from the record store and the ticks this process ran.</summary>
    /// <param name="scope">The profile's settings and repos, and the caller.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>One row per feed, in source order, or the error the scope, settings or store give.</returns>
    public ValueTask<Result<IReadOnlyList<SourceProgress>>> Progress(ContextScope scope, CancellationToken cancellationToken);
}
