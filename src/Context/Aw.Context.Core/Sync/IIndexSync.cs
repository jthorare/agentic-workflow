using Aw.Context.Core.Engine;
using Aw.Context.Core.Records;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
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

    /// <summary>Runs one tick of the named source's feed only, under its lock and cap; one row. A source that is not a record feed of the profile is SND-CONTEXT-058 (<see cref="ContextErrors.SyncSourceUnknown"/>).</summary>
    /// <param name="scope">The profile's settings and repos, and the caller.</param>
    /// <param name="source">The source whose feed ticks.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>That one feed's row, or the error the scope or settings give, or SND-CONTEXT-058.</returns>
    public ValueTask<Result<SyncReport>> Tick(ContextScope scope, SourceId source, CancellationToken cancellationToken);

    /// <summary>
    /// Runs the decision pass over the records already stored for each feed's source, one source after the other and without the source's lock (it reads the store only, so a tick of the
    /// same source goes on). A source an intent walk owns, or no map yet, gives no row.
    /// </summary>
    /// <param name="scope">The profile's settings and repos, and the caller.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>One row per source that was read, in source order, or the error the scope or settings give.</returns>
    public ValueTask<Result<DecisionReport>> Decide(ContextScope scope, CancellationToken cancellationToken);

    /// <summary>Reads each feed's phase, last tick and milestones from the record store and the ticks this process ran.</summary>
    /// <param name="scope">The profile's settings and repos, and the caller.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>One row per feed, in source order, or the error the scope, settings or store give.</returns>
    public ValueTask<Result<IReadOnlyList<SourceProgress>>> Progress(ContextScope scope, CancellationToken cancellationToken);

    /// <summary>Counts what one source holds in the record store, for the purge's preview. Any source id: a source the profile dropped still has rows to purge.</summary>
    /// <param name="source">The source.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Its records, chunks and vectors, or the error the store gives.</returns>
    public ValueTask<Result<SourceCounts>> Counts(SourceId source, CancellationToken cancellationToken);

    /// <summary>Deletes everything one source gave: its records, links, chunks, vectors and position (the human's 'index purge'). A later sync refills only what the source still holds.</summary>
    /// <param name="source">The source.</param>
    /// <param name="cancellationToken">Cancels the purge.</param>
    /// <returns>How many records were deleted, or the error the store gives.</returns>
    public ValueTask<Result<int>> Purge(SourceId source, CancellationToken cancellationToken);
}
