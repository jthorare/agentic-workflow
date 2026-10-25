using System.Globalization;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Packing;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Trust;

namespace Aw.Context.Features.QueryContext;

/// <summary>
/// Turns the graph's sections and the fetched records into the result (spec §7, §8.3, §8.4): the graph's items and the map's
/// citation first, then one 'record' item and one citation per record, every field from a record scrubbed on its own
/// before it is written (title, text, key, ids, versions, aliases, permalink, and the degraded sources' messages; a
/// blocked field is '[REDACTED:blocked]' in that field only), untrusted text inside the fence, then grouped into sections (every item kept; there is no token budget).
/// </summary>
/// <param name="egress">The per-field scrubber.</param>
/// <param name="queryId">The query's id.</param>
/// <param name="asOf">When the answer is assembled.</param>
internal sealed class ResultAssembly(IEgressScrubber egress, QueryId queryId, DateTimeOffset asOf)
{
    internal ContextResult Build(ContextQuery query, RecordPart records, GraphPart graph)
    {
        var cites = new Dictionary<string, Citation>(graph.Cites, StringComparer.Ordinal);
        var candidates = new List<PackCandidate>(graph.Candidates);
        foreach (var record in records.Fetched)
        {
            var marker = "C" + (cites.Count + 1).ToString(CultureInfo.InvariantCulture);
            cites[marker] = Cite(record, records.Versions);
            candidates.Add(new PackCandidate("records", Item(record, marker), marker));
        }

        var recordMarkers = cites.Where(c => c.Key != GraphAnswer.MapMarker).GroupBy(c => c.Value.Key.Value, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First().Key, StringComparer.Ordinal);
        candidates = [.. candidates.Select(c => CiteRecord(c, graph, recordMarkers))];
        var conflicts = graph.Conflicts.Select(c => c with { Intent = [.. c.Intent.Select(k => MarkerOf(k, graph, recordMarkers)).Distinct(StringComparer.Ordinal)] }).ToList();
        var packed = Packer.Pack(candidates);
        return new ContextResult(ContextResult.CurrentSchemaVersion, queryId, query, graph.Anchors, graph.MapVersion, asOf, packed.Sections,
            conflicts, graph.Suggested, [.. records.Degraded.Select(d => d with { Message = egress.Scrub(d.Message).Text })],
            cites, graph.Diff)
        {
            SkippedComponents = graph.SkippedComponents.Count == 0 ? null : graph.SkippedComponents,
        };
    }

    // A decision's quote comes from a record: when the answer carries that record's citation, the decision cites it instead of the map (spec §8.4).
    private static string MarkerOf(string decision, GraphPart graph, Dictionary<string, string> recordMarkers) =>
        graph.DecisionRecords.TryGetValue(decision, out var record) && recordMarkers.TryGetValue(record, out var marker) ? marker : GraphAnswer.MapMarker;

    private static PackCandidate CiteRecord(PackCandidate candidate, GraphPart graph, Dictionary<string, string> recordMarkers)
    {
        if (candidate.Section != "decisions" || candidate.Item.Node is not { } decision)
        {
            return candidate;
        }

        var marker = MarkerOf(decision, graph, recordMarkers);
        return marker == candidate.Marker ? candidate : candidate with { Item = candidate.Item with { Cites = [marker] }, Marker = marker };
    }

    private ResultItem Item(FetchedRecord record, string marker)
    {
        var source = record.Source.Describe().Id.Value;
        var title = UntrustedFence.Wrap(source, author: null, UntrustedFence.Cap(egress.Scrub(record.Title.Text).Text));   // titles are record text too (§7)
        var text = record.Text.Blocked ? null : UntrustedFence.Wrap(source, author: null, egress.Scrub(record.Text.Text).Text);
        return new ResultItem("record", Node: null, title, text, Math.Round(record.Item.Score, 6, MidpointRounding.ToEven), record.Item.Why, Provenance.Deterministic,
            Confirmed: false, Trust.Untrusted, [marker])
        { Tokens = TokenEstimate.Of(title) + TokenEstimate.Of(text) };
    }

    private Citation Cite(FetchedRecord record, IReadOnlyDictionary<SourceRef, (bool Ran, SourceVersion? Current)> versions)
    {
        var refs = new List<CitationRef>();
        var states = new List<CitationState>();
        foreach (var hit in record.Item.Hits)
        {
            var (state, current) = CitationStates.Of(hit.Ref.Version, versions.GetValueOrDefault(hit.Ref, (false, null)), record.Text.Blocked);
            states.Add(state);
            refs.Add(new CitationRef(hit.Ref.Source, ExternalId.From(Scrubbed(hit.Ref.Id.Value)), Scrubbed(hit.Ref.Version)) { Current = current is { } c ? Scrubbed(c) : null, FetchedAt = asOf });
        }

        var best = record.Item.Hits[0];
        return new Citation(RecordKey.From(Scrubbed(record.Item.Key.Value)), refs, Permalink(best.Ref.Permalink), Trust.Untrusted, Worst(states))
        {
            Aliases = Aliases(record.Aliases),
        };
    }

    /// <summary>
    /// Each alias scrubbed on its own, as a title is (§7: aliases are record text): a redacted one keeps its placeholder,
    /// a blocked or empty one is left out (an alias that can't be shown names nothing), and repeats collapse.
    /// </summary>
    private List<string> Aliases(IReadOnlyList<ScrubbedField> cleaned) =>
        [.. cleaned.Where(a => !a.Blocked).Select(a => egress.Scrub(a.Text)).Where(a => !a.Blocked && a.Text.Length > 0)
            .Select(a => UntrustedFence.Cap(a.Text)).Distinct(StringComparer.Ordinal)];

    /// <summary>An id or key scrubbed like any other field; a blocked one keeps a fixed, valid placeholder.</summary>
    private string Scrubbed(string value) => egress.Scrub(value) is { Blocked: false } scrubbed ? scrubbed.Text : "scrub:blocked";

    private SourceVersion Scrubbed(SourceVersion version) => SourceVersion.From(Scrubbed(version.Value));

    /// <summary>
    /// The link, scrubbed as written and as it reads unescaped, then allowed by host; one the scrub changes at all
    /// (redacted or blocked) is dropped, never rewritten: a link with a placeholder in it leads nowhere and could mislead.
    /// </summary>
    private Uri? Permalink(Uri? link) =>
        link is not null && Unchanged(link.OriginalString) && Unchanged(Uri.UnescapeDataString(link.OriginalString)) ? Permalinks.Allowed(link) : null;

    private bool Unchanged(string value) => egress.Scrub(value) is { Blocked: false } scrubbed && string.Equals(scrubbed.Text, value, StringComparison.Ordinal);

    /// <summary>The citation's state is its worst reference's: blocked, then stale, then unchecked, then current.</summary>
    private static CitationState Worst(List<CitationState> states) =>
        new[] { CitationState.ScrubBlocked, CitationState.Stale, CitationState.Unchecked, CitationState.Unstable }.FirstOrDefault(states.Contains, CitationState.Current);
}
