using Aw.Context.Core;
using Aw.Context.Core.Audience;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Features.RecordAccess;
using Aw.Kernel;

namespace Aw.Context.Features.QueryContext;

internal sealed partial class QueryPipeline
{
    // The record store answering with no hit is not an answer of the catalog's sources: every one of them missing is still SND-CONTEXT-011.
    // Under a pin, sources left out on purpose aren't missing: only a source that failed makes it SND-CONTEXT-011, otherwise the answer is empty with its note.
    private static bool NothingAnswered(Searched searched, GraphPart graph) =>
        (graph.Pins.Scope is null || searched.Degraded.Count > 0) && NothingAnswered(searched);

    private static bool NothingAnswered(Searched searched) => searched.Answers.All(a => (a.Source == RecordStoreSource.Id || a.Source == DenseRecordSource.Id) && a.Hits.Count == 0);

    private static Error NoSourceAnswered(IEnumerable<DegradedSource> degraded) =>
        ContextErrors.AllSourcesUnavailable.With($"no source answered ({string.Join(", ", degraded.Select(d => d.Source.Value))}); fix: sindri context sources status");

    /// <summary>
    /// The records of the session's scoped map (spec §4.4 rule (c)): the 'record:' nodes the repo scope kept because a link ties them to its graph nodes. A map that can't be read lists none (default deny).
    /// </summary>
    private static async ValueTask<IReadOnlySet<RecordKey>> LinkedRecords(QueryContextRequest request, MapRead read, IMapProvider graphs, CancellationToken ct)
    {
        HashSet<RecordKey> none = [];
        if (request.Scope.Caller.Kind != ContextCallerKind.Session)
        {
            return none;
        }

        var map = await graphs.Read(request.Scope, read, ct).ConfigureAwait(false);
        return map.IsError ? none : RecordMask.Linked(map.Value.Graph);
    }

    /// <summary>
    /// The catalog's sources and, for a live query with terms, the record store's lexical and dense legs as two more sources (spec §6.1), under the audiences the approved
    /// profile gives the repos in scope (a human also reads the profile's Slack channels). A policy that can't answer drops the leg and says so, as a degraded source.
    /// </summary>
    private async Task<(List<IContextSource> Sources, List<DegradedSource> Degraded)> WithRecordStore(List<IContextSource> catalogSources, (ContextSettings Settings, IReadOnlyList<ContextRepo> Repos) profile,
        (DateTimeOffset? AsOf, IReadOnlyList<string> Terms) ask,
        (QueryContextRequest Request, IMapProvider Graphs, MapRead Read) caller, CancellationToken ct)
    {
        var degraded = new List<DegradedSource>();
        if (ask.AsOf is not null || ask.Terms.Count == 0)
        {
            return (catalogSources, degraded);   // the store keeps no past: a backtest arm reads the sources alone
        }

        var allowed = await RecordMask.Allowed(profile.Settings, profile.Repos, caller.Request.Scope.Caller.Kind, ct).ConfigureAwait(false);
        if (allowed.IsError)
        {
            degraded.Add(SourceCalls.Degraded(RecordStoreSource.Id, allowed.Error, clock.GetUtcNow()));
            return (catalogSources, degraded);
        }

        var audience = new AudienceScope(allowed.Value) { Records = await LinkedRecords(caller.Request, caller.Read, caller.Graphs, ct).ConfigureAwait(false) };
        var teams = profile.Settings.LinearTeams.ToHashSet(StringComparer.Ordinal);
        return ([.. catalogSources, new RecordStoreSource(lexical.Leg, lexical.Store, audience, clock, teams), new DenseRecordSource(lexical, audience, clock, teams)], degraded);
    }
}
