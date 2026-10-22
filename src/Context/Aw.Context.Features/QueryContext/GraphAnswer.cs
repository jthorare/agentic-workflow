using System.Globalization;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Linking;
using Aw.Context.Core.Map;
using Aw.Context.Core.Packing;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;

namespace Aw.Context.Features.QueryContext;

/// <summary>
/// The graph's sections of an answer (spec §8.3, §8.4): 'graph' (each anchor and its own scope, a facts line each),
/// 'affects' (the cascade, with depth and the shortest path), 'decisions' (decisions that decide those nodes) and 'code'
/// (the code units implementing them). Every item cites the map version it was read from (C1). An affects item is as
/// weak as its weakest link: model on any node or edge of its path (both ends of every edge, and the node reached) makes it model, and it is confirmed only when every one is.
/// </summary>
internal static class GraphAnswer
{
    internal const string MapMarker = "C1";

    internal static GraphPart Build(VisibleMap map, IReadOnlyList<GraphNode> anchors, IReadOnlyList<NodeKey> suggested, CascadeReach reach, MapText text)
    {
        var graph = map.Graph;
        var nodes = graph.Nodes.ToDictionary(n => n.Key.Value, StringComparer.Ordinal);
        var own = new List<(NodeKey Key, NodeKey Anchor)>();
        var affected = new Dictionary<string, AffectedNode>(StringComparer.Ordinal);
        var skipped = new Dictionary<string, SkippedComponent>(StringComparer.Ordinal);
        foreach (var anchor in anchors)
        {
            var cascade = Cascade.Affects(graph, anchor.Key, reach.Depth, reach.Options);
            foreach (var unit in cascade.SkippedComponents)
            {
                skipped[unit.Unit] = unit;
            }

            own.AddRange(cascade.OwnScope.Where(k => !own.Exists(o => o.Key.Value == k.Value)).Select(k => (k, anchor.Key)));
            foreach (var a in cascade.Affected.Where(a => !affected.TryGetValue(a.Node.Value, out var known) || Better(a, known)))
            {
                affected[a.Node.Value] = a;
            }
        }

        foreach (var key in own.Select(o => o.Key.Value))
        {
            _ = affected.Remove(key);   // one anchor's own scope is never another's affected node
        }

        var reached = own.Select(o => o.Key.Value).Concat(affected.Keys).ToHashSet(StringComparer.Ordinal);
        var candidates = new List<PackCandidate>();
        candidates.AddRange(own.Select(o => Fact(nodes[o.Key.Value], graph, text, o.Key.Value == o.Anchor.Value ? "anchor" : "own-scope of " + o.Anchor.Value)));
        candidates.AddRange(affected.Values.OrderBy(a => a.Depth).ThenBy(a => a.Node.Value, StringComparer.Ordinal).Select(a => Affects(a, nodes, text)));
        candidates.AddRange(Decisions(graph, reached, text));
        candidates.AddRange(Code(graph, reached, text));
        var cites = new Dictionary<string, Citation>(StringComparer.Ordinal);
        if (candidates.Count > 0)
        {
            cites[MapMarker] = Cite(map, candidates);
        }

        return new GraphPart(candidates, cites, [.. anchors.Select(a => a.Key.Value)], map.Snapshot.Label, [.. suggested.Select(k => k.Value)]) { Conflicts = Conflicts(map, reached, text), DecisionRecords = DecisionRecords(graph, reached), LinkedRecords = LinkedRecords(graph, anchors, reached), RecordSignals = RecordLinks.Of(graph, Depths(own, affected), anchors), SkippedComponents = [.. skipped.Values.OrderBy(u => u.Unit, StringComparer.Ordinal).Select(u => u with { Unit = text.Line(u.Unit) })] };
    }

    private static Dictionary<string, int> Depths(List<(NodeKey Key, NodeKey Anchor)> own, Dictionary<string, AffectedNode> affected) =>
        own.Select(o => (Node: o.Key.Value, Depth: 0)).Concat(affected.Values.Select(a => (Node: a.Node.Value, a.Depth))).GroupBy(d => d.Node, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Min(d => d.Depth), StringComparer.Ordinal);

    private static Dictionary<string, string> DecisionRecords(GraphSnapshot graph, HashSet<string> reached)
    {
        var deciding = graph.Edges.Where(e => e.Type == EdgeType.Decides && reached.Contains(e.To.Value)).Select(e => e.From.Value).ToHashSet(StringComparer.Ordinal);
        return graph.Decisions.Select(d => (Key: d.ToKey().Value, Record: d.Record.Value)).Where(d => deciding.Contains(d.Key)).ToDictionary(d => d.Key, d => d.Record, StringComparer.Ordinal);
    }

    private const string RecordPrefix = "record:";

    private static List<string> LinkedRecords(GraphSnapshot graph, IReadOnlyList<GraphNode> anchors, HashSet<string> reached) =>
        [.. anchors.Select(a => a.Key.Value).Concat(graph.Edges.Where(e => e.Type == EdgeType.Mentions && reached.Contains(e.To.Value)).Select(e => e.From.Value))
            .Where(k => k.StartsWith(RecordPrefix, StringComparison.Ordinal)).Select(k => k[RecordPrefix.Length..]).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];

    // Two decisions on a reached node that a model check found contradicting: the answer lists them by the decisions' item ids (spec §8.4, A-F13).
    private static List<ResultConflict> Conflicts(VisibleMap map, HashSet<string> reached, MapText text)
    {
        var decides = map.Graph.Edges.Where(e => e.Type == EdgeType.Decides && reached.Contains(e.To.Value)).ToLookup(e => e.From.Value, e => e.To.Value, StringComparer.Ordinal);
        return [.. map.Conflicts.Where(c => c.Verdict == ConflictVerdict.Contradicts)
            .SelectMany(c => decides[c.A.Value].Intersect(decides[c.B.Value], StringComparer.Ordinal).Select(node => (Node: node, Pair: c)))
            .OrderBy(x => x.Node, StringComparer.Ordinal).ThenBy(x => x.Pair.A.Value, StringComparer.Ordinal).ThenBy(x => x.Pair.B.Value, StringComparer.Ordinal)
            .Select(x => new ResultConflict(x.Node, [x.Pair.A.Value, x.Pair.B.Value], [], text.Line($"{x.Pair.A.Value[..Math.Min(17, x.Pair.A.Value.Length)]} contradicts {x.Pair.B.Value[..Math.Min(17, x.Pair.B.Value.Length)]}")))];
    }

    // Between anchors the shallower path wins; at equal depth the one with fewer owns hops (as Cascade orders one anchor's), then the key the path starts from.
    private static bool Better(AffectedNode candidate, AffectedNode known)
    {
        var byDepth = candidate.Depth.CompareTo(known.Depth);
        if (byDepth != 0)
        {
            return byDepth < 0;
        }

        var byOwns = Cascade.OwnsHops(candidate.Path).CompareTo(Cascade.OwnsHops(known.Path));
        return byOwns != 0 ? byOwns < 0 : string.CompareOrdinal(candidate.Path[0], known.Path[0]) < 0;
    }

    private static PackCandidate Fact(GraphNode node, GraphSnapshot graph, MapText text, string why)
    {
        var fact = text.Fact(node);
        var key = node.Key.Value;
        var facts = graph.Edges.Where(e => e.From.Value == key && e.Type is not EdgeType.Implements and not EdgeType.Tests)
            .Select(e => $"{EdgeRules.Name(e.Type)} {e.To.Value}")
            .Concat(graph.Edges.Where(e => e.To.Value == key && e.Type == EdgeType.Implements).Select(e => $"implemented-by {e.From.Value}"))
            .Order(StringComparer.Ordinal).Take(12).ToList();
        var line = facts.Count == 0 ? null : text.Line(string.Join("; ", facts));
        return new PackCandidate("graph", Item(fact, line, (fact.Provenance, fact.Confirmed, fact.Trust), why), MapMarker);
    }

    private static PackCandidate Affects(AffectedNode affected, Dictionary<string, GraphNode> nodes, MapText text)
    {
        var node = nodes[affected.Node.Value];
        var onPath = affected.Edges.SelectMany(e => new[] { e.From.Value, e.To.Value }).Append(affected.Node.Value).Distinct(StringComparer.Ordinal)
            .Select(k => nodes.TryGetValue(k, out var n) ? n.Provenance : Provenance.Human);   // why: a cut-away node can't weaken what the caller sees
        var provenances = affected.Edges.Select(e => e.Provenance).Concat(onPath).ToList();
        var provenance = provenances.Contains(Provenance.Model) ? Provenance.Model : provenances.Contains(Provenance.Deterministic) ? Provenance.Deterministic : Provenance.Human;
        var confirmed = provenances.TrueForAll(p => p == Provenance.Human);
        var trust = TrustPolicy.ForNode(node.Key.Kind, provenance, confirmed);   // the cascade never reaches a record today (Cascade.Steps ends at one), so this is the rule for the day it does; a record's title stays fenced however trusted its path
        var fact = text.Fact(node);
        var path = affected.Path.Select(text.Line).ToList();
        var item = Item(fact with { Label = trust == fact.Trust ? fact.Label : text.Label(node.Label, trust) }, text: null, (provenance, confirmed, trust),
            string.Create(CultureInfo.InvariantCulture, $"cascade depth {affected.Depth}"));
        return new PackCandidate("affects", item with { Depth = affected.Depth, Path = path, Tokens = item.Tokens + path.Sum(TokenEstimate.Of) }, MapMarker);
    }

    private static IEnumerable<PackCandidate> Decisions(GraphSnapshot graph, HashSet<string> reached, MapText text) =>
        graph.Decisions
            .Select(d => (Row: d, Key: d.ToKey()))
            .Select(d => (d.Row, d.Key, Decides: graph.Edges.Where(e => e.Type == EdgeType.Decides && e.From.Value == d.Key.Value && reached.Contains(e.To.Value)).ToList()))
            .Where(d => d.Decides.Count > 0)
            .OrderBy(d => d.Key.Value, StringComparer.Ordinal)
            .Select(d =>
            {
                var confirmed = d.Row.Provenance == Provenance.Human;
                var trust = TrustPolicy.For(d.Row.Provenance, confirmed);
                var item = new ResultItem("decision", d.Key.Value, text.Label(d.Row.Wording, trust), text.Body(d.Row.Quote, Trust.Untrusted), Score: null,
                    [.. d.Decides.Select(e => "decides→" + e.To.Value)], d.Row.Provenance, confirmed, trust, [MapMarker]);
                return new PackCandidate("decisions", item with { Tokens = TokenEstimate.Of(item.Title) + TokenEstimate.Of(item.Text) }, MapMarker);
            });

    private static IEnumerable<PackCandidate> Code(GraphSnapshot graph, HashSet<string> reached, MapText text) =>
        graph.Edges.Where(e => e.Type == EdgeType.Implements && reached.Contains(e.To.Value))
            .GroupBy(e => e.From.Value, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g =>
            {
                var provenance = g.Any(e => e.Provenance == Provenance.Model) ? Provenance.Model : g.All(e => e.Provenance == Provenance.Human) ? Provenance.Human : Provenance.Deterministic;
                var confirmed = provenance == Provenance.Human;
                var trust = TrustPolicy.For(provenance, confirmed);
                var targets = g.Select(e => e.To.Value).Order(StringComparer.Ordinal).ToList();
                var item = new ResultItem("code", g.Key, text.Line(g.Key["code:".Length..]), text.Line("implements " + string.Join(", ", targets)), Score: null,
                    [.. targets.Select(t => "implements→" + t)], provenance, confirmed, trust, [MapMarker]);
                return new PackCandidate("code", item with { Tokens = TokenEstimate.Of(item.Title) + TokenEstimate.Of(item.Text) }, MapMarker);
            });

    private static ResultItem Item(NodeFact fact, string? text, (Provenance Provenance, bool Confirmed, Trust Trust) facts, string why)
    {
        var item = new ResultItem(fact.Key.Value[..fact.Key.Value.IndexOf(':', StringComparison.Ordinal)], fact.Key.Value, fact.Label, text, Score: null, [why],
            facts.Provenance, facts.Confirmed, facts.Trust, [MapMarker]);
        return item with { Tokens = TokenEstimate.Of(item.Title) + TokenEstimate.Of(item.Text) };
    }

    /// <summary>The map's citation: its version as the record's version; trusted only when every item citing it is.</summary>
    private static Citation Cite(VisibleMap map, List<PackCandidate> candidates)
    {
        var number = string.Create(CultureInfo.InvariantCulture, $"v{map.Snapshot.Version.Number}");
        var trust = candidates.TrueForAll(c => c.Item.Trust == Trust.Trusted) ? Trust.Trusted : Trust.Untrusted;
        return new Citation(RecordKey.From($"map:{map.Snapshot.Version.Repo.Value}/{number}"),
            [new CitationRef(SourceId.From("map"), ExternalId.From(number), SourceVersion.From(map.Snapshot.Label)) { FetchedAt = map.Snapshot.Version.Created }], Permalink: null, trust, CitationState.Current);
    }
}
