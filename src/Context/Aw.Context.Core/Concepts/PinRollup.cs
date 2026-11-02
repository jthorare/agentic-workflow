using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;

namespace Aw.Context.Core.Concepts;

/// <summary>
/// Rolls the feature roll-up edges up to the pinned concepts (spec 2026-10-27 §10.2): P depends-on Q when a feature of P depends on one of Q, P reads or writes a thing when a feature of P does. Deterministic, a
/// <see cref="GraphEdge.Count"/> of the feature edges behind it and the first <see cref="FeatureRollup.EvidencePairs"/> evidence refs. Pinned concepts only. A declared edge of the same key is merged into the rolled one: one Human edge with the count.
/// </summary>
public static class PinRollup
{
    /// <summary>The concept edges of a build.</summary>
    /// <param name="graph">The map with its concepts and its feature roll-up edges.</param>
    /// <param name="pins">Every pin of the build.</param>
    /// <param name="declared">The edges the pins declare.</param>
    /// <returns>The edges, one per key, and the notes.</returns>
    public static PinRollupResult Apply(GraphSnapshot graph, IReadOnlyList<ConceptPin> pins, DeclaredEdges declared)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(pins);
        ArgumentNullException.ThrowIfNull(declared);
        var resolved = PinIds.Resolve(pins);
        var rolled = Rolled(graph, [.. resolved.Select(p => p.Id)]);
        var rolledKeys = rolled.ToDictionary(Key);
        var declaredKeys = declared.Edges.ToDictionary(Key);
        var edges = new List<GraphEdge>();
        edges.AddRange(rolled.Where(e => !declaredKeys.ContainsKey(Key(e))));
        edges.AddRange(declared.Edges.Select(d => rolledKeys.TryGetValue(Key(d), out var seen) ? Merged(d, seen) : d));
        return new PinRollupResult(edges, Notes(resolved, rolled, declared.Edges));
    }

    private static (string, string, EdgeType) Key(GraphEdge e) => (e.From.Value, e.To.Value, e.Type);

    // A declared edge the code also shows is one edge: Human, the roll-up's count, the profile key first and then the files that show it.
    private static GraphEdge Merged(GraphEdge declared, GraphEdge seen) =>
        declared with { Count = seen.Count, Evidence = [.. declared.EvidenceOrEmpty.Concat(seen.EvidenceOrEmpty).Take(FeatureRollup.EvidencePairs)] };

    private static List<GraphEdge> Rolled(GraphSnapshot graph, List<string> ids)
    {
        var pinOf = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var id in ids)
        {
            foreach (var member in graph.Edges.Where(e => e is { Type: EdgeType.BelongsTo, From.Kind: NodeKind.Feature } && e.To.Value == "concept:" + id))
            {
                _ = pinOf.TryAdd(member.From.Value, id);
            }
        }

        var groups = new Dictionary<(string From, string To, EdgeType Type), List<GraphEdge>>();
        foreach (var edge in graph.Edges.Where(e => e.Count is not null && e.From.Kind == NodeKind.Feature && pinOf.ContainsKey(e.From.Value)))
        {
            if (Target(edge, pinOf) is not { } to)
            {
                continue;
            }

            var group = ("concept:" + pinOf[edge.From.Value], to, edge.Type);
            if (!groups.TryGetValue(group, out var members))
            {
                groups[group] = members = [];
            }

            members.Add(edge);
        }

        return [.. groups.OrderBy(g => g.Key.From, StringComparer.Ordinal).ThenBy(g => g.Key.To, StringComparer.Ordinal).ThenBy(g => g.Key.Type)
            .Select(g => new GraphEdge(NodeKey.From(g.Key.From), NodeKey.From(g.Key.To), g.Key.Type, Provenance.Deterministic, 1.0,
                [.. g.Value.SelectMany(e => e.EvidenceOrEmpty).Take(FeatureRollup.EvidencePairs)], Count: g.Value.Sum(e => e.Count ?? 0)))];
    }

    // What a feature edge rolls up to: the other pin for a 'depends-on' (none inside one pin or to an unpinned feature), the thing for a 'reads' or 'writes'.
    private static string? Target(GraphEdge edge, Dictionary<string, string> pinOf)
    {
        if (edge.Type is EdgeType.Reads or EdgeType.Writes)
        {
            return edge.To.Value;
        }

        return edge.Type == EdgeType.DependsOn && pinOf.TryGetValue(edge.To.Value, out var other) && other != pinOf[edge.From.Value] ? "concept:" + other : null;
    }

    private static List<string> Notes(IReadOnlyList<ResolvedPin> pins, List<GraphEdge> rolled, IReadOnlyList<GraphEdge> declared)
    {
        var notes = new List<string>();
        var seen = rolled.Select(e => (e.From.Value, e.To.Value, e.Type)).ToHashSet();
        var said = declared.Select(e => (e.From.Value, e.To.Value, e.Type)).ToHashSet();
        notes.AddRange(declared.Where(e => !seen.Contains((e.From.Value, e.To.Value, e.Type))).Select(e => $"declared, not observed: {Line(e)}"));
        var declaring = pins.Where(p => p.Pin.DependsOn.Count > 0 || p.Pin.Owns.Count > 0).Select(p => "concept:" + p.Id).ToHashSet(StringComparer.Ordinal);
        notes.AddRange(rolled.Where(e => e.Type == EdgeType.DependsOn && declaring.Contains(e.From.Value) && !said.Contains((e.From.Value, e.To.Value, e.Type)))
            .Select(e => $"observed, not declared: {Line(e)} ({e.Count} imports)"));
        return notes;
    }

    private static string Line(GraphEdge e) => $"{e.From.Name} {EdgeRules.Name(e.Type)} {e.To.Name}";
}
