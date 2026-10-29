using Aw.Context.Core.Engine;
using Aw.Context.Core.Records;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Context.Core.Trust;
using Aw.Kernel;

namespace Aw.Context.Features.SyncSources;

/// <summary>
/// <see cref="IIndexSync"/> over the record feeds (owned-index spec §5.5, §5.8): the same feeds and the same per-source tick as <see cref="SyncSourcesService"/>, without the
/// intent walk or the docs refresh. A source that is down is its row's code; the last tick's end and code are kept in memory, so they read null until the first tick after a start.
/// </summary>
/// <param name="feeds">The feeds and their lock and last outcomes.</param>
/// <param name="store">The record store the positions are read from.</param>
/// <param name="scrubberFactory">Builds the call's scrubbers.</param>
internal sealed class IndexSync(RecordFeedSync feeds, IRecordStore store, IScrubberFactory scrubberFactory) : IIndexSync
{
    public async ValueTask<Result<SyncReport>> Tick(ContextScope scope, CancellationToken cancellationToken)
    {
        var resolved = Resolve(scope);
        if (resolved.IsError)
        {
            return resolved.Error;
        }

        var rows = new List<SourceSync>();
        foreach (var feed in resolved.Value.Feeds)
        {
            var row = await feeds.Run(feed, (resolved.Value.Scrubbers, resolved.Value.Settings), afterTick: null, cancellationToken).ConfigureAwait(false);
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

            var last = feeds.LastOutcome(feed.Source);
            rows.Add(SourceProgress.From(feed.Source, position.Value.Phase, last?.At, last?.Code));
        }

        return rows;
    }

    public ValueTask<Result<SourceCounts>> Counts(SourceId source, CancellationToken cancellationToken) => store.Count(source, cancellationToken);

    public ValueTask<Result<int>> Purge(SourceId source, CancellationToken cancellationToken) => feeds.Exclusive(source, () => store.Purge(source, cancellationToken));

    private Result<(ContextSettings Settings, Scrubbers Scrubbers, IReadOnlyList<RecordFeed> Feeds)> Resolve(ContextScope scope)
    {
        var settings = SettingsReader.Read(scope.Settings);
        var scrubbers = settings.Bind(s => scrubberFactory.For(s.ExtraPatterns));
        var repos = scrubbers.Bind(_ => ScopeRules.Resolve(settings.Value, scope.Repos, scope.Caller));
        return repos.Bind(r => Results.Success((settings.Value, scrubbers.Value, feeds.For(settings.Value, r, scrubbers.Value))));
    }
}
