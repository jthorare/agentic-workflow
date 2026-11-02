using Aw.Context.Core.Engine;
using Aw.Context.Core.Trust;

namespace Aw.Context.Core.Sync;

/// <summary>The feeds of sources that live outside Context (owned-index spec §5.2): the Host implements it over each connector, and the catalog adds what it returns to its own feeds.</summary>
public interface IExternalRecordFeeds
{
    /// <summary>The feeds for <paramref name="repos"/> under <paramref name="settings"/>.</summary>
    /// <param name="settings">The checked settings.</param>
    /// <param name="repos">The repos in scope.</param>
    /// <param name="scrubbers">The call's scrubbers.</param>
    /// <returns>The feeds; none when the source isn't configured or its token doesn't resolve.</returns>
    public IReadOnlyList<RecordFeed> For(ContextSettings settings, IReadOnlyList<ContextRepo> repos, Scrubbers scrubbers);
}
