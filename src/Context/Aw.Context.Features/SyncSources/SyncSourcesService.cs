using Aw.Context.Core;
using Aw.Context.Core.Docs;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Linking;
using Aw.Context.Core.Map;
using Aw.Context.Core.Models;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Context.Core.Trust;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.SyncSources;

/// <summary>
/// Syncs the live intent sources (spec §8.8, S-5B): each is walked against the current map by <see cref="IntentSyncRun"/>. A source that is down is a row
/// with its code, not a failure of the call; a second sync of a source that is already running is SND-CONTEXT-056, and a source that isn't a live intent source
/// of this profile SND-CONTEXT-058. It runs as the engine itself (spec §3): a session's reading rules don't apply, so the links it writes are the same whoever
/// asks. The repo docs are refreshed by the facade next to it (ContextSources). Next to the intent walk, every record feed of the profile (<see cref="IRecordFeedCatalog"/>) runs one
/// tick of <see cref="RecordSyncRun"/> into the record store, after the intent rows; a source with both (Linear) has two rows. The two keep separate positions (the intent sync's
/// row at repo '*', the feed's at 'records').
/// </summary>
internal sealed class SyncSourcesService(
    ISourceCatalog catalog, RecordFeedSync feeds, IScrubberFactory scrubberFactory, MapReader graphs, IIntentStore intents, IModelPort model, TimeProvider clock)
    : IService<SyncSourcesRequest, SyncReport>
{
    /// <summary>The longest one run may take before it stops and saves its cursor (spec §8.8).</summary>
    internal static readonly TimeSpan RunCap = TimeSpan.FromMinutes(10);

    public async ValueTask<Result<SyncReport>> Handle(SyncSourcesRequest request, CancellationToken cancellationToken)
    {
        var settings = SettingsReader.Read(request.Scope.Settings);
        var scrubbers = settings.Bind(s => scrubberFactory.For(s.ExtraPatterns));
        var repos = scrubbers.Bind(_ => ScopeRules.Resolve(settings.Value, request.Scope.Repos, request.Scope.Caller));
        var wanted = repos.Bind(r => Wanted(request.Source, catalog.For(settings.Value, r, scrubbers.Value), feeds.For(settings.Value, r, scrubbers.Value)));
        if (wanted.IsError)
        {
            return wanted.Error;
        }

        var rows = new List<SourceSync>();
        var intent = await IntentRows(wanted.Value.Intent, request.Scope, (settings.Value, scrubbers.Value), cancellationToken).ConfigureAwait(false);
        if (intent.IsError)
        {
            return intent.Error;
        }

        rows.AddRange(intent.Value);
        var fed = await FeedRows(wanted.Value.Feeds, wanted.Value.Intent, request.Scope, (settings.Value, scrubbers.Value), cancellationToken).ConfigureAwait(false);
        if (fed.IsError)
        {
            return fed.Error;
        }

        rows.AddRange(fed.Value);
        return new SyncReport(rows);
    }

    /// <summary>
    /// One tick of each feed, each followed by its decision pass over the stored records, against the map read once (no map: no pass); a source the intent walk reads in this call (Linear) keeps its
    /// decisions from that walk, so its feed gets no pass (two versions of one key would requeue each other, one model call each time).
    /// </summary>
    private async ValueTask<Result<List<SourceSync>>> FeedRows(IReadOnlyList<RecordFeed> wanted, IReadOnlyList<IContextSource> walked, ContextScope scope, (ContextSettings Settings, Scrubbers Scrubbers) context, CancellationToken cancellationToken)
    {
        var rows = new List<SourceSync>();
        if (wanted.Count == 0)
        {
            return rows;
        }

        var map = await graphs.ReadWithoutIntent(scope, draft: false, cancellationToken).ConfigureAwait(false);   // no map yet (the first build needs these records): the feeds still sync, the pass waits for one
        foreach (var feed in wanted)
        {
            var decisions = map.IsError || walked.Any(w => w.Describe().Id == feed.Source) ? null : new RecordDecisionRun(feed.Source, map.Value, context, (feeds.OwnedRecords(), intents, model, clock));
            var row = await feeds.Run(feed, (context.Scrubbers, context.Settings), decisions is null ? null : tick => decisions.Execute(tick, cancellationToken), cancellationToken).ConfigureAwait(false);
            if (row.IsError)
            {
                return row.Error;
            }

            rows.Add(row.Value);
        }

        return rows;
    }

    private async ValueTask<Result<List<SourceSync>>> IntentRows(IReadOnlyList<IContextSource> wanted, ContextScope scope, (ContextSettings Settings, Scrubbers Scrubbers) context, CancellationToken cancellationToken)
    {
        var rows = new List<SourceSync>();
        if (wanted.Count == 0)
        {
            return rows;
        }

        var map = await graphs.ReadWithoutIntent(scope, draft: false, cancellationToken).ConfigureAwait(false);
        if (map.IsError)
        {
            return map.Error;
        }

        foreach (var source in wanted)
        {
            var row = await feeds.Locked(source.Describe().Id, () => new IntentSyncRun(source, map.Value, context, (intents, model, clock)).Execute(cancellationToken)).ConfigureAwait(false);
            if (row.IsError)
            {
                return row.Error;
            }

            rows.Add(row.Value);
        }

        return rows;
    }

    private static Result<(IReadOnlyList<IContextSource> Intent, IReadOnlyList<RecordFeed> Feeds)> Wanted(SourceId? named, IReadOnlyList<IContextSource> all, IReadOnlyList<RecordFeed> feeds)
    {
        var live = all.Where(s => s.Describe() is { Kind: SourceKind.Intent, Freshness: SourceFreshness.Live }).OrderBy(s => s.Describe().Id.Value, StringComparer.Ordinal).ToList();
        IReadOnlyList<IContextSource> intent = [.. live.Where(s => named is null || s.Describe().Id == named)];
        IReadOnlyList<RecordFeed> chosen = [.. feeds.Where(f => named is null || f.Source == named)];
        return named is { } name && intent.Count == 0 && chosen.Count == 0
            ? ContextErrors.SyncSourceUnknown.With($"'{name.Value}' isn't a source this profile syncs ({string.Join(", ", live.Select(s => s.Describe().Id.Value).Concat(feeds.Select(f => f.Source.Value)).Distinct(StringComparer.Ordinal))}); fix: sindri context sources status")
            : Results.Success((intent, chosen));
    }
}
