using Aw.Context.Core.Engine;
using Aw.Context.Core.Eval;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;

namespace Aw.Context.Tests.Eval;

/// <summary>Hand-made answers for scoring tests: items by section, each cited by the map citation 'C1'.</summary>
internal static class Answers
{
    public static readonly DateTimeOffset At = new(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);

    public static ResultItem Item(string? node, params string[] cites) =>
        new(node?.Split(':')[0] ?? "record", node, Title: null, Text: null, Score: null, Why: [], Provenance.Human, Confirmed: true, Trust.Trusted, cites.Length == 0 ? ["C1"] : cites);

    public static ContextResult Of(params (string Section, ResultItem Item)[] items) => With(Map(), items);

    public static ContextResult With(IReadOnlyDictionary<string, Citation> cites, params (string Section, ResultItem Item)[] items) =>
        new(ContextResult.CurrentSchemaVersion, QueryId.From("q_test"), new ContextQuery("q"), [], "v1", At,
            [.. items.GroupBy(i => i.Section, StringComparer.Ordinal).Select(g => new ResultSection(g.Key, [.. g.Select(i => i.Item)]))],
            [], [], [], cites);

    public static Dictionary<string, Citation> Map(params (string Marker, string Key)[] more)
    {
        var cites = new Dictionary<string, Citation>(StringComparer.Ordinal) { ["C1"] = Cite("map:aw-split-panes/v1") };
        foreach (var (marker, key) in more)
        {
            cites[marker] = Cite(key);
        }

        return cites;
    }

    public static Citation Cite(string key) =>
        new(RecordKey.From(key), [new CitationRef(SourceId.From("map"), ExternalId.From("v1"), SourceVersion.From("v1"))], Permalink: null, Trust.Trusted, CitationState.Current);

    public static ProbeCase Probe(string id, string[]? nodes = null, string[]? absent = null, string[]? records = null, string[]? files = null) =>
        new(ProbeId.From(id), "question " + id, new ProbeExpect(nodes, absent, records, files));
}
