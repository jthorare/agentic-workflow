using Aw.Context.Core.Docs;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Linking;
using Aw.Context.Core.Map;
using Aw.Context.Core.Models;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Context.Core.Trust;

namespace Aw.Context.Features.SyncSources;

/// <summary>
/// The decision pass that follows a record feed's tick (<see cref="RecordDecisionRun"/>), built one way for the manual sync and the index tick: the map is read once (no map yet: no
/// pass, the feeds still sync) and a feed whose source an intent walk owns gets none (two versions of one key would requeue each other, one model call each time). A record that
/// hasn't changed isn't sent to the model again; <see cref="RecordDecisionRun"/> sees to that.
/// </summary>
/// <param name="feeds">The feeds, for the record store the pass reads.</param>
/// <param name="catalog">The profile's sources, for the ones the intent walk reads.</param>
/// <param name="graphs">Reads the map the pass quotes against.</param>
/// <param name="intents">The decision store.</param>
/// <param name="model">The model the pass asks.</param>
/// <param name="clock">The clock.</param>
internal sealed class FeedDecisions(RecordFeedSync feeds, ISourceCatalog catalog, MapReader graphs, IIntentStore intents, IModelPort model, TimeProvider clock)
{
    // why: the Linear intent walk already resolves linear:project/<id>, so a pass over the project feed would write the same keys twice
    private static readonly SourceId _projects = SourceId.From(LinearSourceIds.Projects);

    /// <summary>The step a feed's tick is followed by, or null for none.</summary>
    internal delegate Func<SourceSync, ValueTask<SourceSync>>? AfterTick(RecordFeed feed);

    /// <summary>The steps for each feed of one call; <paramref name="walked"/> are the sources whose decisions an intent walk owns.</summary>
    internal async ValueTask<AfterTick> For(ContextScope scope, (ContextSettings Settings, Scrubbers Scrubbers) context, IReadOnlyList<IContextSource> walked, CancellationToken cancellationToken)
    {
        var map = await graphs.ReadWithoutIntent(scope, draft: false, cancellationToken).ConfigureAwait(false);   // no map yet (the first build needs these records): the feeds still sync, the pass waits for one
        return feed =>
        {
            if (map.IsError || feed.Source == _projects || walked.Any(w => w.Describe().Id == feed.Source))
            {
                return null;
            }

            var decisions = new RecordDecisionRun(feed.Source, map.Value, context, (feeds.OwnedRecords(), intents, model, clock));
            return tick => decisions.Execute(tick, cancellationToken);
        };
    }

    /// <summary>
    /// The steps for the index tick, which walks no intent sources itself: the live intent sources of the profile (Linear) keep the decisions of the manual sync's walk.
    /// </summary>
    internal ValueTask<AfterTick> ForTick(ContextScope scope, (ContextSettings Settings, Scrubbers Scrubbers) context, IReadOnlyList<ContextRepo> repos, CancellationToken cancellationToken)
    {
        IReadOnlyList<IContextSource> live = [.. catalog.For(context.Settings, repos, context.Scrubbers).Where(s => s.Describe() is { Kind: SourceKind.Intent, Freshness: SourceFreshness.Live })];
        return For(scope, context, live, cancellationToken);
    }
}
