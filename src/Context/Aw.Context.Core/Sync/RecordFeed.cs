using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Core.Sync;

/// <summary>
/// A source's record feed, the one shape the sync driver pulls (spec §5.2): Linear and repo docs implement it inside Context,
/// the Host adapter implements it over each connector. It replaces <see cref="IChangeFeed"/>. Replaying a pull answers the
/// same page; a source that is down is an error with its code.
/// </summary>
/// <param name="source">The source this feed reads.</param>
public abstract class RecordFeed(SourceId source)
{
    /// <summary>The source this feed reads; the keys it returns belong to it.</summary>
    public SourceId Source { get; } = source;

    /// <summary>Pulls one page.</summary>
    /// <param name="pull">Where to continue, how much and how far back.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The page; an empty page ends the feed.</returns>
    public abstract ValueTask<Result<RecordPage>> Pull(RecordPull pull, CancellationToken cancellationToken);
}
