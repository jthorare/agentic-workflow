using Aw.Context.Core.Engine;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Linking;
using Aw.Context.Core.Map;
using Aw.Context.Core.Models;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Context.Core.Trust;
using Microsoft.Extensions.Logging;

namespace Aw.Context.Features.SyncSources;

/// <summary>
/// The decision pass that follows a record feed's tick (<see cref="RecordDecisionRun"/>), built one way for the manual sync and the index tick: the map is read once (no map yet: no
/// pass, the feeds still sync). A record that
/// hasn't changed isn't sent to the model again; <see cref="RecordDecisionRun"/> sees to that.
/// </summary>
/// <param name="feeds">The feeds, for the record store the pass reads.</param>
/// <param name="graphs">Reads the map the pass quotes against.</param>
/// <param name="intents">The decision store.</param>
/// <param name="model">The model the pass asks.</param>
/// <param name="clock">The clock.</param>
/// <param name="logger">Where a record skipped for linking is warned about; none logs nothing.</param>
internal sealed class FeedDecisions(RecordFeedSync feeds, MapReader graphs, IIntentStore intents, IModelPort model, TimeProvider clock, ILogger<FeedDecisions>? logger = null)
{
    // why: the project feed's records are not read for decisions
    private static readonly SourceId _projects = LinearSourceIds.Projects;

    /// <summary>The step a feed's tick is followed by, or null for none.</summary>
    internal delegate Func<SourceSync, ValueTask<SourceSync>>? AfterTick(RecordFeed feed);

    /// <summary>The steps for each feed of one call, a missing map being no step.</summary>
    internal ValueTask<AfterTick> For(ContextScope scope, (ContextSettings Settings, Scrubbers Scrubbers) context, CancellationToken cancellationToken) =>
        For(scope, context, reportNoMap: false, cancellationToken);

    /// <summary>The steps for each feed of one call; with <paramref name="reportNoMap"/>, a missing map is the step's code instead of no step.</summary>
    internal async ValueTask<AfterTick> For(ContextScope scope, (ContextSettings Settings, Scrubbers Scrubbers) context, bool reportNoMap, CancellationToken cancellationToken)
    {
        var map = await graphs.ReadWithoutIntent(scope, draft: false, cancellationToken).ConfigureAwait(false);   // no map yet (the first build needs these records): the feeds still sync, the pass waits for one
        var pins = PinAliases.Of(ScopeRules.Mapped(context.Settings, scope.Repos).SelectMany(r => r.Concepts));   // the pins the build used, so concept:<id> is its node
        return feed =>
        {
            if (map.IsError && reportNoMap && feed.Source != _projects)
            {
                // why: the decision run says it skipped and why; the feed's own sync is not at fault, so its tick row stays as it is
                return tick => ValueTask.FromResult(tick with { Code = map.Error.Code });
            }

            if (map.IsError || feed.Source == _projects)
            {
                return null;
            }

            var decisions = new RecordDecisionRun(feed.Source, map.Value, context, (feeds.OwnedRecords(), intents, model, clock), pins, logger);
            return tick => decisions.Execute(tick, cancellationToken);
        };
    }
}
