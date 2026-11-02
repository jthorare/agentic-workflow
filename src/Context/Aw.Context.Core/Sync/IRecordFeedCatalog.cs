using Aw.Context.Core.Engine;
using Aw.Context.Core.Trust;

namespace Aw.Context.Core.Sync;

/// <summary>Builds the record feeds the sync driver pulls (spec §5.2): one per source that lives inside Context, over the repos in the caller's scope.</summary>
public interface IRecordFeedCatalog
{
    /// <summary>The feeds for <paramref name="repos"/> under <paramref name="settings"/>.</summary>
    /// <param name="settings">The checked settings.</param>
    /// <param name="repos">The repos in scope.</param>
    /// <param name="scrubbers">The call's scrubbers: what a feed cleans, it cleans with them.</param>
    /// <returns>The feeds, in a stable order; none when no repo is in scope.</returns>
    public IReadOnlyList<RecordFeed> For(ContextSettings settings, IReadOnlyList<ContextRepo> repos, Scrubbers scrubbers);
}
