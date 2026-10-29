using Aw.Context.Core;
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
/// Syncs the record feeds of the profile (<see cref="IRecordFeedCatalog"/>): one tick of <see cref="RecordSyncRun"/> each into the record store, followed by its decision pass. A second sync of a
/// source that is already running is SND-CONTEXT-056, and a source that isn't a feed of this profile SND-CONTEXT-058. It runs as the engine itself (spec §3): a session's reading rules don't
/// apply, so the links it writes are the same whoever asks. The repo docs are refreshed by the facade next to it (ContextSources).
/// </summary>
internal sealed class SyncSourcesService(
    RecordFeedSync feeds, IScrubberFactory scrubberFactory, MapReader graphs, IIntentStore intents, IModelPort model, TimeProvider clock)
    : IService<SyncSourcesRequest, SyncReport>
{
    /// <summary>The longest one run may take before it stops and saves its cursor (spec §8.8).</summary>
    internal static readonly TimeSpan RunCap = TimeSpan.FromMinutes(10);

    public async ValueTask<Result<SyncReport>> Handle(SyncSourcesRequest request, CancellationToken cancellationToken)
    {
        var settings = SettingsReader.Read(request.Scope.Settings);
        var scrubbers = settings.Bind(s => scrubberFactory.For(s.ExtraPatterns));
        var repos = scrubbers.Bind(_ => ScopeRules.Resolve(settings.Value, request.Scope.Repos, request.Scope.Caller));
        var wanted = repos.Bind(r => Wanted(request.Source, feeds.For(settings.Value, r, scrubbers.Value)));
        if (wanted.IsError)
        {
            return wanted.Error;
        }

        var rows = new List<SourceSync>();
        var fed = await FeedRows(wanted.Value, request.Scope, (settings.Value, scrubbers.Value), cancellationToken).ConfigureAwait(false);
        if (fed.IsError)
        {
            return fed.Error;
        }

        rows.AddRange(fed.Value);
        return new SyncReport(rows);
    }

    /// <summary>
    /// One tick of each feed, each followed by its decision pass over the stored records, against the map read once (no map: no pass).
    /// </summary>
    private async ValueTask<Result<List<SourceSync>>> FeedRows(IReadOnlyList<RecordFeed> wanted, ContextScope scope, (ContextSettings Settings, Scrubbers Scrubbers) context, CancellationToken cancellationToken)
    {
        var rows = new List<SourceSync>();
        if (wanted.Count == 0)
        {
            return rows;
        }

        var after = await new FeedDecisions(feeds, graphs, intents, model, clock).For(scope, context, cancellationToken).ConfigureAwait(false);
        foreach (var feed in wanted)
        {
            var row = await feeds.Run(feed, (context.Scrubbers, context.Settings), after(feed), cancellationToken).ConfigureAwait(false);
            if (row.IsError)
            {
                return row.Error;
            }

            if (row.Value.Code is { } code)
            {
                _ = await intents.RecordFailure(feed.Source, code, cancellationToken).ConfigureAwait(false);   // why: 'context sources status' and the doctor's context:sync row read the code from the intent state; a write that fails is dropped, the row already carries it
            }

            rows.Add(row.Value);
        }

        return rows;
    }

    private static Result<IReadOnlyList<RecordFeed>> Wanted(SourceId? named, IReadOnlyList<RecordFeed> feeds)
    {
        IReadOnlyList<RecordFeed> chosen = [.. feeds.Where(f => named is null || f.Source == named)];
        return named is { } name && chosen.Count == 0
            ? ContextErrors.SyncSourceUnknown.With($"'{name.Value}' isn't a source this profile syncs ({string.Join(", ", feeds.Select(f => f.Source.Value).Distinct(StringComparer.Ordinal))}); fix: sindri context sources status")
            : Results.Success(chosen);
    }
}
