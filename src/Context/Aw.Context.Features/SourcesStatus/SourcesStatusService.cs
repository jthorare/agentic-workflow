using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Docs;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Linking;
using Aw.Context.Core.Records;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Trust;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.SourcesStatus;

/// <summary>
/// The sources' state: one repo-docs row per repo in scope, from its index (never refreshed reads as degraded, a
/// refresh that failed as unavailable, documents still stored under other scrub rules as degraded until the next
/// refresh), then every live source's probe. A session's view is gated and its repo rows scoped like its queries, but
/// says whether sessions are enabled; it lists the live sources it doesn't search too, health only, each saying so.
/// </summary>
internal sealed class SourcesStatusService(IDocIndex index, ISourceCatalog catalog, IScrubberFactory scrubberFactory, IIntentStore intents, IRecordStore records, TimeProvider clock)
    : IService<SourcesStatusRequest, SourcesView>
{
    public async ValueTask<Result<SourcesView>> Handle(SourcesStatusRequest request, CancellationToken cancellationToken)
    {
        var settings = SettingsReader.Read(request.Scope.Settings);
        var scrubbers = settings.Bind(s => scrubberFactory.For(s.ExtraPatterns));
        var repos = scrubbers.Bind(_ => ScopeRules.Resolve(settings.Value, request.Scope.Repos, request.Scope.Caller));
        if (repos.IsError)
        {
            return repos.Error;
        }

        var rows = new List<SourceStatusRow>();
        foreach (var repo in repos.Value)
        {
            var status = await index.Status(repo, scrubbers.Value.Ruleset, cancellationToken).ConfigureAwait(false);
            if (status.IsError)
            {
                return status.Error;
            }

            rows.Add(Row(repo.Name, status.Value));
        }

        var live = await LiveRows(catalog.For(settings.Value, repos.Value, scrubbers.Value).Where(static s => s.Describe().Freshness == SourceFreshness.Live), cancellationToken)
            .ConfigureAwait(false);
        if (live.IsError)
        {
            return live.Error;
        }

        rows.AddRange(live.Value);
        return new SourcesView(rows, settings.Value.SessionsEnabled, clock.GetUtcNow());
    }

    // Every live source is listed, a session's view too (a session searches the intent sources like a human, owner q93, 2026-10-23).
    private async ValueTask<Result<IReadOnlyList<SourceStatusRow>>> LiveRows(IEnumerable<IContextSource> sources, CancellationToken cancellationToken)
    {
        var rows = new List<SourceStatusRow>();
        foreach (var source in sources)
        {
            var health = await source.Probe(cancellationToken).ConfigureAwait(false);
            var described = source.Describe();
            var row = new SourceStatusRow(described.Id, described.Kind, Repo: null, health.State, health.Detail, health.Code, LastSync: null, Records: 0);
            if (described.Kind == SourceKind.Intent)
            {
                var state = await intents.State(described.Id, cancellationToken).ConfigureAwait(false);
                if (state.IsError)
                {
                    return state.Error;
                }

                // why: the feed's tick writes its end and code to the record store, one row per source that each tick sets and a good tick clears (RecordFeedSync.Note)
                var outcome = await records.LastOutcome(described.Id, cancellationToken).ConfigureAwait(false);
                row = row with { Sync = SyncStatus(state.Value, outcome.IsError ? null : outcome.Value) };
            }

            rows.Add(row);
        }

        return rows;
    }

    /// <summary>
    /// Unavailable after a failed tick; labeling being off or over its token cap (042) only queues the extraction, so the links synced and it stays ok. A tick the time cap stopped (059) is partial,
    /// not failed. The tick's end and code come from the record store, so a good tick after a failed one is ok again; the counts come from the intent state. While records are queued and
    /// the tick has no code the code stays 042, run after run (a run that fetches nothing records no failure of its own).
    /// </summary>
    private static SourceSyncStatus SyncStatus(IntentSourceState state, SyncOutcome? outcome)
    {
        var failed = Failed(outcome?.Code);
        var lastSync = outcome?.At ?? state.LastSync;
        var health = failed ? SourceState.Unavailable : lastSync is null ? SourceState.Degraded : SourceState.Ok;
        return new SourceSyncStatus(lastSync, health, state.Records, state.Links, state.Decisions, state.Queued, outcome?.Code ?? (state.Queued > 0 ? ContextErrors.LabelingUnavailable.Code : null));
    }

    private static bool Failed(string? code) => code is not null && code != ContextErrors.LabelingUnavailable.Code && code != ContextErrors.SyncCapped.Code;

    private static SourceStatusRow Row(ContextRepoName repo, DocIndexStatus status)
    {
        var head = status.Head is { } h ? h.Value[..Math.Min(7, h.Value.Length)] : "none";
        var (state, detail) = (status.LastSync, status.Code) switch
        {
            (null, null) => (SourceState.Degraded, "never refreshed (fix: name the context-docs-refresh service in the profile)"),
            (_, { } code) => (SourceState.Unavailable, $"the last refresh failed ({code})"),
            _ when status.Outdated > 0 => (SourceState.Degraded, string.Create(CultureInfo.InvariantCulture,
                $"{status.Outdated} of {status.Documents} documents wait for the next refresh: the scrub rules changed, and searches skip them until they are cleaned again")),
            _ => (SourceState.Ok, string.Create(CultureInfo.InvariantCulture, $"{status.Documents} documents, {status.Chunks} chunks at {head}")),
        };
        return new SourceStatusRow(RepoDocs.Id, SourceKind.Doc, repo, state, detail, status.Code, status.LastSync, status.Chunks);
    }
}
