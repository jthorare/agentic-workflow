using System.Text.Json;
using Aw.Context.Core.ConceptSynthesis;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;
using GraphConceptKeys = Aw.Context.Core.Graph.ConceptKeys;

namespace Aw.Context.Core.Concepts;

/// <summary>
/// Drops the derived concepts a synthesized one covers (#1281): a derived concept goes, with every edge on it, when one synthesized concept holds at least the cover share of its features.
/// Its features keep their synthesized membership and the rest fall to 'other' on read. Pins, the catch-all and a derived concept that shares a synthesized key are never dropped.
/// </summary>
public static class CoveredConcepts
{
    /// <summary>Drops the covered derived concepts.</summary>
    /// <param name="graph">The graph with the derived and the synthesized concepts.</param>
    /// <param name="concepts">Every stored synthesized concept; only the 'done' ones cover.</param>
    /// <param name="share">The share of a derived concept's features one synthesized concept must hold (0.5 to 1).</param>
    /// <returns>The graph without the covered concepts, in the same order otherwise.</returns>
    public static CoverFold Fold(GraphSnapshot graph, IReadOnlyList<SynthesizedConcept> concepts, double share)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(concepts);
        var synthesized = concepts.Where(c => c.State == SynthesisState.Done).Select(c => "concept:" + c.Key).ToHashSet(StringComparer.Ordinal);
        var features = Features(graph);
        var covering = graph.Nodes.Where(n => synthesized.Contains(n.Key.Value) && !IsPin(n)).Select(n => features.GetValueOrDefault(n.Key.Value) ?? []).ToList();
        var covered = graph.Nodes
            .Where(n => IsDerived(n) && !synthesized.Contains(n.Key.Value) && features.TryGetValue(n.Key.Value, out var own) && own.Count > 0
                && covering.Exists(c => c.Count(own.Contains) >= (share * own.Count) - 1e-9))
            .Select(n => n.Key.Value)
            .ToHashSet(StringComparer.Ordinal);
        if (covered.Count == 0)
        {
            return new CoverFold(graph, 0);
        }

        return new CoverFold(
            graph with
            {
                Nodes = [.. graph.Nodes.Where(n => !covered.Contains(n.Key.Value))],
                Edges = [.. graph.Edges.Where(e => !covered.Contains(e.From.Value) && !covered.Contains(e.To.Value))],
            },
            covered.Count);
    }

    // Each concept's member features, by concept key.
    private static Dictionary<string, HashSet<string>> Features(GraphSnapshot graph)
    {
        var byConcept = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var edge in graph.Edges.Where(e => e.Type == EdgeType.BelongsTo && e.From.Kind == NodeKind.Feature && e.To.Kind == NodeKind.Concept))
        {
            if (!byConcept.TryGetValue(edge.To.Value, out var members))
            {
                byConcept[edge.To.Value] = members = new HashSet<string>(StringComparer.Ordinal);
            }

            _ = members.Add(edge.From.Value);
        }

        return byConcept;
    }

    private static bool IsDerived(GraphNode node) =>
        node.Key.Kind == NodeKind.Concept && node.Key.Value != GraphConceptKeys.Other && node.Provenance != Provenance.Human && Source(node) == "derived";

    private static bool IsPin(GraphNode node) => node.Provenance == Provenance.Human || Source(node) == "override";

    private static string? Source(GraphNode node) =>
        node.Attributes is { ValueKind: JsonValueKind.Object } attributes && attributes.TryGetProperty("source", out var source) && source.ValueKind == JsonValueKind.String ? source.GetString() : null;
}
