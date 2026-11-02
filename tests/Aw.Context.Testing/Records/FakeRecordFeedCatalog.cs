using Aw.Context.Core.Engine;
using Aw.Context.Core.Sync;
using Aw.Context.Core.Trust;

namespace Aw.Context.Testing.Records;

/// <summary>The <see cref="IRecordFeedCatalog"/> over a fixed list of feeds; with no repo in scope it answers none, as the real catalog does.</summary>
/// <param name="feeds">The feeds it answers.</param>
public sealed class FakeRecordFeedCatalog(params RecordFeed[] feeds) : IRecordFeedCatalog
{
    /// <inheritdoc />
    public IReadOnlyList<RecordFeed> For(ContextSettings settings, IReadOnlyList<ContextRepo> repos, Scrubbers scrubbers)
    {
        ArgumentNullException.ThrowIfNull(repos);
        return repos.Count == 0 ? [] : feeds;
    }
}
