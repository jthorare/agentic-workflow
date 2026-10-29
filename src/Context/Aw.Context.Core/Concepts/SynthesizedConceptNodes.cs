using System.Buffers;
using System.Text.Json;
using Aw.Context.Core.ConceptSynthesis;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Concepts;

/// <summary>Puts the synthesized concepts (the cross-source groups the model named) into a map graph as 'concept:&lt;key&gt;' nodes with their evidence and 'belongs-to' edges.</summary>
public static class SynthesizedConceptNodes
{
    private const double Confidence = 0.7;

    /// <summary>Adds a node for each concept the model has labeled, or merges its evidence into the node already there, and joins the member records and the features they touch to it.</summary>
    /// <param name="graph">The graph so far, with the derived concepts.</param>
    /// <param name="concepts">Every stored synthesized concept; only the 'done' ones count.</param>
    /// <returns>The graph with the concepts, in the same order for the same input.</returns>
    public static GraphSnapshot Apply(GraphSnapshot graph, IReadOnlyList<SynthesizedConcept> concepts)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(concepts);
        var done = concepts.Where(c => c.State == SynthesisState.Done).OrderBy(c => c.Key, StringComparer.Ordinal).ToList();
        if (done.Count == 0)
        {
            return graph;
        }

        var nodes = graph.Nodes.ToList();
        var edges = graph.Edges.ToList();
        var seen = edges.Select(e => (e.From.Value, e.To.Value, e.Type)).ToHashSet();
        foreach (var concept in done)
        {
            var key = NodeKey.From("concept:" + concept.Key);
            var members = Members(concept);
            var evidence = members.Select(m => new EvidenceRef(m)).ToList();
            var at = nodes.FindIndex(n => n.Key.Value == key.Value);
            if (at >= 0)
            {
                // why: a derived or human concept of the same key keeps its label and provenance; the model's members only add evidence.
                var existing = nodes[at];
                nodes[at] = existing with { Evidence = [.. existing.EvidenceOrEmpty.Concat(evidence).DistinctBy(e => (e.Key.Value, e.Span))] };
            }
            else
            {
                nodes.Add(new GraphNode(key, concept.Name, Provenance.Model, Confidence, evidence, Attributes(concept.Summary)));
            }

            foreach (var from in Joined(graph, members))
            {
                if (EdgeRules.Allows(EdgeType.BelongsTo, from.Kind, NodeKind.Concept) && seen.Add((from.Value, key.Value, EdgeType.BelongsTo)))
                {
                    edges.Add(new GraphEdge(from, key, EdgeType.BelongsTo, Provenance.Model, Confidence));
                }
            }
        }

        return graph with { Nodes = nodes, Edges = edges };
    }

    // The members' record keys, valid ones only, distinct and in order.
    private static List<RecordKey> Members(SynthesizedConcept concept) =>
        [.. concept.Members.Select(m => RecordKey.TryFrom(m.Record, out var key) ? (RecordKey?)key : null).OfType<RecordKey>().DistinctBy(k => k.Value, StringComparer.Ordinal).OrderBy(k => k.Value, StringComparer.Ordinal)];

    // The member record nodes in the graph and every feature linked to one of them by an edge either way or citing a member, in key order.
    private static IEnumerable<NodeKey> Joined(GraphSnapshot graph, List<RecordKey> members)
    {
        var records = members.Select(m => "record:" + m.Value).ToHashSet(StringComparer.Ordinal);
        var cited = members.Select(m => m.Value).ToHashSet(StringComparer.Ordinal);
        var kinds = graph.Nodes.ToDictionary(n => n.Key.Value, n => n.Key, StringComparer.Ordinal);
        var linked = new HashSet<string>(StringComparer.Ordinal);
        foreach (var edge in graph.Edges)
        {
            if (records.Contains(edge.From.Value) && edge.To.Kind == NodeKind.Feature)
            {
                _ = linked.Add(edge.To.Value);
            }
            else if (records.Contains(edge.To.Value) && edge.From.Kind == NodeKind.Feature)
            {
                _ = linked.Add(edge.From.Value);
            }
        }

        foreach (var node in graph.Nodes.Where(n => n.Key.Kind == NodeKind.Feature && n.EvidenceOrEmpty.Any(e => cited.Contains(e.Key.Value))))
        {
            _ = linked.Add(node.Key.Value);
        }

        return records.Where(kinds.ContainsKey).Concat(linked.Where(kinds.ContainsKey)).Order(StringComparer.Ordinal).Select(k => kinds[k]);
    }

    private static JsonElement Attributes(string summary)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("summary", summary);
            writer.WriteString("source", "synthesized");
            writer.WriteString("seed", "");
            writer.WriteEndObject();
        }

        using var document = JsonDocument.Parse(buffer.WrittenMemory);
        return document.RootElement.Clone();
    }
}
