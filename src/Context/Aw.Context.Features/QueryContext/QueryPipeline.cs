using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Linking;
using Aw.Context.Core.Map;
using Aw.Context.Core.Models;
using Aw.Context.Core.Ranking;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Trust;
using Aw.Kernel;
using Aw.Kernel.Ids;

namespace Aw.Context.Features.QueryContext;

/// <summary>
/// The query pipeline (spec §8.2): scope and gate; anchors from the map (the node shape's key, alias or name; the text
/// shape's whole node names) with their graph sections (<see cref="GraphAnswer"/>); terms (the question's, or the node
/// anchor's name); every source searched under the
/// deadline (a missing source is degraded; all of them missing is SND-CONTEXT-011), canonical dedup and RRF, the top
/// items fetched and cleaned once at the ingest boundary, one batched version check per source, then packing,
/// citations and per-field egress scrubbing (<see cref="ResultAssembly"/>). Ingest and egress both use this call's
/// scrubbers, built from the approved profile's extra patterns, and so does the map's text. A text question asked with no
/// map (or by a session whose repo implements none of it) is answered from the sources alone. No model is called unless 'context.ranker.rerank' is on (Phase 8: <see cref="RankerStage"/>).
/// </summary>
internal sealed partial class QueryPipeline(IScrubberFactory scrubberFactory, IIdGenerator ids, TimeProvider clock, IIntentStore intents, IModelPort model)
{
    private readonly RankerStage _ranker = new(model, clock);

    /// <summary>The most fused items fetched (and so cited) per query.</summary>
    internal const int MaxItems = 20;

    /// <summary>The most text kept from one record after cleaning (spec §8.3: 64 KB).</summary>
    internal const int MaxRecordText = 64 * 1024;

    /// <summary>Answers one question over the given map and catalog.</summary>
    /// <param name="request">The question and its scope.</param>
    /// <param name="inputs">Where the map and the sources come from: the live ones for a query, an as-of view for a backtest arm.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The packed result, or the query's error.</returns>
    public async ValueTask<Result<ContextResult>> Run(QueryContextRequest request, PipelineInputs inputs, CancellationToken cancellationToken)
    {
        var (graphs, catalog) = (inputs.Map, inputs.Catalog);
        var settings = SettingsReader.Read(request.Scope.Settings);
        var scrubbers = settings.Bind(s => scrubberFactory.For(s.ExtraPatterns));
        var repos = scrubbers.Bind(_ => ScopeRules.Resolve(settings.Value, request.Scope.Repos, request.Scope.Caller));
        var valid = repos.Bind(_ => CheckedQuery.Of(request.Query, settings.Value));
        if (valid.IsError)
        {
            return valid.Error;
        }

        var anchored = await AnchorFor(request, valid.Value, (settings.Value, scrubbers.Value), inputs, cancellationToken).ConfigureAwait(false);
        if (anchored.IsError)
        {
            return anchored.Error;
        }

        var (graph, terms, miss) = anchored.Value;
        var deadline = clock.GetUtcNow() + settings.Value.Deadline;
        var sources = terms.Count == 0 ? [] : catalog.For(settings.Value, repos.Value, scrubbers.Value).Where(s => ScopeRules.MayRead(request.Scope.Caller, s.Describe())).ToList();
        var searched = await Search(sources, terms, (deadline, inputs.AsOf), cancellationToken).ConfigureAwait(false);
        if (sources.Count > 0 && searched.Answers.Count == 0 && graph.Anchors.Count == 0)
        {
            return ContextErrors.AllSourcesUnavailable.With(
                $"no source answered ({string.Join(", ", searched.Degraded.Select(d => d.Source.Value))}); fix: sindri context sources status");
        }

        var degraded = searched.Degraded.ToList();
        var fused = await Ranked(searched, (graph, RankerPlan.For(settings.Value.Ranker, inputs.Ranker)), scrubbers.Value.Egress, degraded, cancellationToken).ConfigureAwait(false);
        var fetched = await Fetch(fused, (sources, scrubbers.Value.Ingest), deadline, degraded, cancellationToken).ConfigureAwait(false);
        if (miss is not null && !Answers(fetched, miss.Item.Trim()))
        {
            return Unknown(miss, request.Scope);   // not linked by any sync, and no live record carries that tracker key
        }

        await NoteQueuedDecisions(graph, sources, degraded, cancellationToken).ConfigureAwait(false);
        var versions = await Versions(fetched, sources, deadline, cancellationToken).ConfigureAwait(false);
        var assembly = new ResultAssembly(scrubbers.Value.Egress, QueryId.From("q_" + ids.NewId().ToString("N", CultureInfo.InvariantCulture)), clock.GetUtcNow());
        return assembly.Build(request.Query, valid.Value.Valid.Budget, new RecordPart(fetched, versions, degraded), graph);
    }

    /// <summary>
    /// The anchors and their graph sections, and the terms the sources are searched with. The node shape needs the map
    /// (030 without one, 003 for an unknown name, 037 for an ambiguous one); a text question goes on without it unless it
    /// asked for the draft or the store failed.
    /// </summary>
    private static async ValueTask<Result<(GraphPart Graph, IReadOnlyList<string> Terms)>> Anchored(QueryContextRequest request, CheckedQuery checkedQuery,
        (ContextSettings Settings, Scrubbers Scrubbers) context, IMapProvider graphs, CancellationToken cancellationToken)
    {
        var (settings, scrubbers) = context;
        var (valid, read) = (checkedQuery.Valid, checkedQuery.Read);
        var query = request.Query;
        var reach = CascadeReach.Of(query, settings);
        var map = await graphs.Read(request.Scope, read, cancellationToken).ConfigureAwait(false);
        if (query.Node is { } node)
        {
            var anchor = map.Bind(m => AnchorMatch.Resolve(m.Graph, node));
            if (anchor.IsError)
            {
                return anchor.Error;
            }

            var text = new MapText(scrubbers);
            return (GraphAnswer.Build(map.Value, [anchor.Value], [], reach, text), text.SearchTerms(anchor.Value.Label));
        }

        if (query.Symbol is { } symbol)
        {
            return Symbolic(map, symbol, reach, new MapText(scrubbers));
        }

        if (map.IsError)
        {
            var unanchored = (map.Error.Code == ContextErrors.NoMap.Code && !query.Draft) || map.Error.Code == ContextErrors.UnknownAnchor.Code;
            return unanchored ? (GraphPart.None, valid.Terms) : map.Error;
        }

        var found = AnchorMatch.FromText(map.Value.Graph, query.Text ?? string.Empty);
        var anchors = found.Anchors.Select(map.Value.Graph.Resolve).OfType<GraphNode>().ToList();
        return (GraphAnswer.Build(map.Value, anchors, found.Suggested, reach, new MapText(scrubbers)), valid.Terms);
    }

    private static async ValueTask<Result<(GraphPart Graph, IReadOnlyList<string> Terms, ItemMiss? Miss)>> AnchorFor(QueryContextRequest request, CheckedQuery checkedQuery,
        (ContextSettings Settings, Scrubbers Scrubbers) context, PipelineInputs inputs, CancellationToken cancellationToken)
    {
        var (graphs, diffs) = (inputs.Map, inputs.Diff);
        if (request.Query.Item is { } item)
        {
            return await ItemAnchored(request, item, context, (graphs, checkedQuery.Read), cancellationToken).ConfigureAwait(false);
        }

        if (request.Query.Diff is { } diff)
        {
            return diffs is null
                ? ContextErrors.QueryInvalid.With("the diff shape is answered by the live query only")
                : (await diffs.Build(request, diff, checkedQuery.Read, context, cancellationToken).ConfigureAwait(false)).Map(a => (a.Graph, a.Terms, (ItemMiss?)null));
        }

        var anchored = await Anchored(request, checkedQuery, context, graphs, cancellationToken).ConfigureAwait(false);
        return anchored.Map(a => (a.Graph, a.Terms, (ItemMiss?)null));
    }

    private static bool Answers(IReadOnlyList<FetchedRecord> fetched, string item) =>
        fetched.Any(f => string.Equals(f.Item.Key.Value, item, StringComparison.Ordinal) || f.Aliases.Any(a => string.Equals(a.Text, item, StringComparison.Ordinal)));

    /// <summary>
    /// The item shape: the record the key names in the map the caller may read (its mentions and its decisions' targets are the anchors), searched
    /// on its alias and those nodes' names. A key no sync linked is looked up live for a human or a service, and is unknown for a session (spec §3 source rule).
    /// </summary>
    private static async ValueTask<Result<(GraphPart Graph, IReadOnlyList<string> Terms, ItemMiss? Miss)>> ItemAnchored(QueryContextRequest request, string item,
        (ContextSettings Settings, Scrubbers Scrubbers) context, (IMapProvider Graphs, MapRead Read) source, CancellationToken cancellationToken)
    {
        var map = await source.Graphs.Read(request.Scope, source.Read, cancellationToken).ConfigureAwait(false);
        var unmapped = map.IsError && !request.Query.Draft && (string.Equals(map.Error.Code, ContextErrors.NoMap.Code, StringComparison.Ordinal) || string.Equals(map.Error.Code, ContextErrors.UnknownAnchor.Code, StringComparison.Ordinal));   // no map, or nothing of it this session's repo implements
        if (map.IsError && !unmapped)
        {
            return map.Error;
        }

        var record = map.IsError ? null : ItemAnchors.Record(map.Value.Graph, item);
        if (record is null)
        {
            var miss = new ItemMiss(item, map.IsError ? [] : ItemAnchors.Suggest(map.Value.Graph, item.Trim()));
            return request.Scope.Caller.Kind == ContextCallerKind.Session ? Unknown(miss, request.Scope) : (GraphPart.None, QueryRules.Terms(item), miss);
        }

        return FoundItem(request, (map.Value, record), item, context);
    }

    private static (GraphPart Graph, IReadOnlyList<string> Terms, ItemMiss? Miss) FoundItem(QueryContextRequest request, (VisibleMap Map, GraphNode Record) found, string item,
        (ContextSettings Settings, Scrubbers Scrubbers) context)
    {
        var text = new MapText(context.Scrubbers);
        var touched = ItemAnchors.Touched(found.Map.Graph, found.Record);
        var terms = QueryRules.Terms(found.Record.Key.Name).Concat(QueryRules.Terms(item)).Concat(touched.Skip(1).SelectMany(n => text.SearchTerms(n.Label))).Distinct(StringComparer.Ordinal).Take(QueryRules.MaxTerms).ToList();
        return (GraphAnswer.Build(found.Map, touched, [], CascadeReach.Of(request.Query, context.Settings), text), terms, null);
    }

    private static Error Unknown(ItemMiss miss, ContextScope scope) => QueryRules.UnknownItem(miss.Item, miss.Suggested, scope.SyncConfigured);

    private async Task<Searched> Search(IReadOnlyList<IContextSource> sources, IReadOnlyList<string> terms, (DateTimeOffset Deadline, DateTimeOffset? AsOf) when, CancellationToken ct)
    {
        var deadline = when.Deadline;
        var calls = sources.Select(async source =>
        {
            var query = new SourceQuery(terms, when.AsOf, Limit: 50, deadline - clock.GetUtcNow());
            var hits = await SourceCalls.Within(source, (query.Deadline, clock), t => source.Search(query, t), ct).ConfigureAwait(false);
            return (source.Describe().Id, hits);
        });
        var answers = new List<SourceAnswer>();
        var degraded = new List<DegradedSource>();
        foreach (var (id, hits) in await Task.WhenAll(calls).ConfigureAwait(false))
        {
            if (hits.IsSuccess)
            {
                answers.Add(new SourceAnswer(id, hits.Value));
            }
            else
            {
                degraded.Add(SourceCalls.Degraded(id, hits.Error, clock.GetUtcNow()));
            }
        }

        return new Searched(answers, degraded);
    }

    private async Task<IReadOnlyList<FetchedRecord>> Fetch(IReadOnlyList<FusedItem> items, (IReadOnlyList<IContextSource> Sources, IIngestFilter Ingest) call,
        DateTimeOffset deadline, List<DegradedSource> degraded, CancellationToken ct)
    {
        var (sources, ingest) = call;
        var fetched = new List<FetchedRecord>();
        foreach (var item in items)
        {
            if (deadline - clock.GetUtcNow() <= TimeSpan.Zero)
            {
                break;   // no claim without a citation: what wasn't fetched in time isn't shown (search already answered)
            }

            var hit = item.Hits[0];
            var source = sources.First(s => s.Describe().Id == hit.Ref.Source);
            var record = await SourceCalls.Within(source, (deadline - clock.GetUtcNow(), clock), t => source.Fetch(hit.Ref, t), ct).ConfigureAwait(false);
            if (record.IsError)
            {
                if (record.Error.Kind != ErrorKind.NotFound && degraded.TrueForAll(d => d.Source != hit.Ref.Source))
                {
                    degraded.Add(SourceCalls.Degraded(hit.Ref.Source, record.Error, clock.GetUtcNow()));
                }

                continue;   // a record gone since the search, or a source that stopped answering: no claim without a citation
            }

            var text = record.Value.Blocked   // the adapter's own whole-document clean withheld it: cite it as scrub-blocked
                ? new ScrubbedField(string.Empty, Redacted: false, Blocked: true)
                : ingest.Clean(record.Value.Text, singleLine: false);   // cleaned whole, then cut, so a cut never splits a secret
            var cut = text with { Text = UntrustedFence.Prefix(text.Text, MaxRecordText) };
            var aliases = item.Aliases.Select(a => ingest.Clean(a, singleLine: true)).ToList();   // record text too: cleaned like the title
            fetched.Add(new FetchedRecord(item, source, ingest.Clean(record.Value.Title, singleLine: true), cut, aliases));
        }

        return fetched;
    }

    /// <summary>
    /// A record linked to the answer's nodes whose decisions are still queued (labeling off or over its cap) is told as degraded 042 for 'decisions',
    /// so a missing decision is not read as no decision (verification plan J2.3). Nothing is asked of the model.
    /// </summary>
    private async Task NoteQueuedDecisions(GraphPart graph, IReadOnlyList<IContextSource> sources, List<DegradedSource> degraded, CancellationToken ct)
    {
        if (graph.LinkedRecords.Count == 0)
        {
            return;
        }

        var waiting = new HashSet<string>(StringComparer.Ordinal);
        foreach (var source in sources.Select(s => s.Describe()).Where(d => d.Kind == SourceKind.Intent))
        {
            var queued = await intents.Queued(source.Id, ct).ConfigureAwait(false);
            waiting.UnionWith(queued.IsError ? [] : queued.Value.Select(k => k.Value));
        }

        if (graph.LinkedRecords.Any(waiting.Contains))
        {
            degraded.Add(new DegradedSource(SourceId.From("decisions"), ContextErrors.LabelingUnavailable.Code,
                "decisions of a linked record are not extracted yet: labeling is off or over its token cap (fix: turn on 'context.labeling.enabled' or raise 'maxTokensPerSync', then run sindri context sources sync, or wait for the next context-sync slot)", clock.GetUtcNow()));
        }
    }

    /// <summary>One batched version check per source, under what is left of the deadline (spec §8.2 step 8).</summary>
    private async Task<IReadOnlyDictionary<SourceRef, (bool Ran, SourceVersion? Current)>> Versions(IReadOnlyList<FetchedRecord> fetched,
        IReadOnlyList<IContextSource> sources, DateTimeOffset deadline, CancellationToken ct)
    {
        var checkedRefs = new Dictionary<SourceRef, (bool, SourceVersion?)>();
        foreach (var group in fetched.SelectMany(f => f.Item.Hits).Select(h => h.Ref).Distinct().GroupBy(r => r.Source))
        {
            var source = sources.First(s => s.Describe().Id == group.Key);   // each ref is checked by its own source
            var refs = group.ToList();
            var left = deadline - clock.GetUtcNow();
            var versions = left <= TimeSpan.Zero
                ? ContextErrors.SourceUnavailable.With("no time left for the version check")
                : await SourceCalls.Within(source, (left, clock), t => source.Versions(refs, t), ct).ConfigureAwait(false);
            for (var i = 0; i < refs.Count; i++)
            {
                checkedRefs[refs[i]] = versions.IsSuccess && versions.Value.Count == refs.Count ? (true, versions.Value[i]) : (false, null);
            }
        }

        return checkedRefs;
    }
}
