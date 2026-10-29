using Aw.Context.Core.Engine;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Records;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Context.Core.Trust;
using Aw.Kernel;

namespace Aw.Context.Features.SyncSources;

/// <summary>
/// <see cref="IIndexSync"/> over the record feeds (owned-index spec §5.5, §5.8): the same feeds and the same per-source tick as <see cref="SyncSourcesService"/>, without the
/// intent walk or the docs refresh; each feed's tick is followed by its decision pass (<see cref="FeedDecisions"/>, #1574) unless a live intent source owns it. A source that is down is its row's code; the last tick's end and code are kept in memory, so they read null until the first tick after a start.
/// </summary>
/// <param name="feeds">The feeds and their lock and last outcomes.</param>
/// <param name="store">The record store the positions are read from.</param>
/// <param name="scrubberFactory">Builds the call's scrubbers.</param>
/// <param name="decisions">The decision pass after each feed's tick; null: none.</param>
/// <param name="steps">The other steps after a feed's tick (the Linear viewer after the projects feed); null: none. They run after the decision pass, in that order.</param>
internal sealed class IndexSync(RecordFeedSync feeds, IRecordStore store, IScrubberFactory scrubberFactory, FeedDecisions? decisions = null, TickSteps? steps = null) : IIndexSync
{
    public async ValueTask<Result<SyncReport>> Tick(ContextScope scope, CancellationToken cancellationToken)
    {
        var resolved = Resolve(scope);
        if (resolved.IsError)
        {
            return resolved.Error;
        }

        var context = (resolved.Value.Settings, resolved.Value.Scrubbers);
        var after = decisions is null || resolved.Value.Feeds.Count == 0 ? null : await decisions.ForTick(scope, context, resolved.Value.Repos, cancellationToken).ConfigureAwait(false);
        var rows = new List<SourceSync>();
        foreach (var feed in resolved.Value.Feeds)
        {
            var row = await feeds.Run(feed, (context.Scrubbers, context.Settings), AfterSteps.Compose(after?.Invoke(feed), steps?.For(feed, context.Settings, cancellationToken)), cancellationToken).ConfigureAwait(false);
            if (row.IsError)
            {
                return row.Error;
            }

            rows.Add(row.Value);
        }

        return new SyncReport(rows);
    }

    public async ValueTask<Result<IReadOnlyList<SourceProgress>>> Progress(ContextScope scope, CancellationToken cancellationToken)
    {
        var resolved = Resolve(scope);
        if (resolved.IsError)
        {
            return resolved.Error;
        }

        var rows = new List<SourceProgress>();
        foreach (var feed in resolved.Value.Feeds)
        {
            var position = await store.Position(feed.Source, cancellationToken).ConfigureAwait(false);
            if (position.IsError)
            {
                return position.Error;
            }

            // why: only the Linear issue feed is reset to Recent with rows already stored (migration 0020), so only it is checked for a re-read; any other source in Recent is on its first read
            int? stored = null;
            if (feed.Source == LinearSourceIds.Issues && position.Value.Phase is SyncPhase.Recent)
            {
                var counted = await store.Count(feed.Source, cancellationToken).ConfigureAwait(false);
                if (counted.IsError)
                {
                    return counted.Error;
                }

                stored = counted.Value.Records;
            }

            var last = feeds.LastOutcome(feed.Source);
            rows.Add(SourceProgress.From(feed.Source, position.Value.Phase, last?.At, last?.Code, stored));
        }

        return rows;
    }

    public ValueTask<Result<SourceCounts>> Counts(SourceId source, CancellationToken cancellationToken) => store.Count(source, cancellationToken);

    public ValueTask<Result<int>> Purge(SourceId source, CancellationToken cancellationToken) => feeds.Exclusive(source, () => store.Purge(source, cancellationToken));

    private Result<(ContextSettings Settings, Scrubbers Scrubbers, IReadOnlyList<ContextRepo> Repos, IReadOnlyList<RecordFeed> Feeds)> Resolve(ContextScope scope)
    {
        var settings = SettingsReader.Read(scope.Settings);
        var scrubbers = settings.Bind(s => scrubberFactory.For(s.ExtraPatterns));
        var repos = scrubbers.Bind(_ => ScopeRules.Resolve(settings.Value, scope.Repos, scope.Caller));
        return repos.Bind(r => Results.Success((settings.Value, scrubbers.Value, r, feeds.For(settings.Value, r, scrubbers.Value))));
    }
}
