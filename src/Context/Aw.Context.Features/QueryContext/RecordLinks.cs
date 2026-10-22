using Aw.Context.Core.Graph;
using Aw.Context.Core.Ranking;

namespace Aw.Context.Features.QueryContext;

/// <summary>Reads, from the map, how each record links to the nodes an answer reached (the ranker's signals, spec §8.2 step 5). Pure.</summary>
internal static class RecordLinks
{
    private const string RecordPrefix = "record:";

    /// <summary>The signals by the record's key.</summary>
    /// <param name="graph">The map's graph.</param>
    /// <param name="depths">The cascade depth of each reached node: 0 for an anchor's own scope.</param>
    /// <param name="anchors">The anchors (an anchor that is itself a record reaches the answer at depth 0).</param>
    /// <returns>One entry per linked record: its nearest link, the strongest on a tie.</returns>
    internal static Dictionary<string, RecordSignals> Of(GraphSnapshot graph, IReadOnlyDictionary<string, int> depths, IReadOnlyList<GraphNode> anchors)
    {
        var decided = graph.Decisions.GroupBy(d => d.ToKey().Value, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First().Record.Value, StringComparer.Ordinal);
        var replaced = graph.Edges.Where(e => e.Type == EdgeType.Supersedes).Select(e => e.To.Value).ToHashSet(StringComparer.Ordinal);
        var found = new Dictionary<string, RecordSignals>(StringComparer.Ordinal);
        foreach (var edge in graph.Edges.Where(e => depths.ContainsKey(e.To.Value)))
        {
            var record = RecordOf(edge, decided);
            if (record is not null)
            {
                var signals = new RecordSignals(depths[edge.To.Value], edge.Type, edge.Provenance, edge.Type == EdgeType.Decides && replaced.Contains(edge.From.Value), edge.At);
                found[record] = found.TryGetValue(record, out var known) ? Nearer(known, signals) : signals;
            }
        }

        foreach (var anchor in anchors.Where(a => a.Key.Value.StartsWith(RecordPrefix, StringComparison.Ordinal)))
        {
            _ = found.TryAdd(anchor.Key.Value[RecordPrefix.Length..], new RecordSignals(CascadeDistance: 0, EdgeType.Mentions, Core.Results.Provenance.Human, Superseded: false, Linked: null));
        }

        return found;
    }

    private static string? RecordOf(GraphEdge edge, Dictionary<string, string> decided)
    {
        if (edge.Type == EdgeType.Decides)
        {
            return decided.GetValueOrDefault(edge.From.Value);
        }

        return edge.Type == EdgeType.Mentions && edge.From.Value.StartsWith(RecordPrefix, StringComparison.Ordinal) ? edge.From.Value[RecordPrefix.Length..] : null;
    }

    private static RecordSignals Nearer(RecordSignals a, RecordSignals b) =>
        (a.CascadeDistance, -RankFeatures.Edge(a.Edge)).CompareTo((b.CascadeDistance, -RankFeatures.Edge(b.Edge))) <= 0 ? a : b;
}
