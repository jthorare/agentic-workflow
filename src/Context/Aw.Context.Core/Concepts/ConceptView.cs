using Aw.Context.Core.Graph;
using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Concepts;

/// <summary>What the deriver reads of one graph: labels, features, and which features each record or decision mentions or decides.</summary>
internal sealed class ConceptView
{
    private readonly Dictionary<string, GraphNode> _nodes;
    private readonly Dictionary<string, List<string>> _featuresOf = new(StringComparer.Ordinal);

    internal ConceptView(GraphSnapshot graph)
    {
        _nodes = graph.Nodes.GroupBy(n => n.Key.Value, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        foreach (var edge in graph.Edges.Where(e => e.Type is EdgeType.Mentions or EdgeType.Decides && e.To.Kind == NodeKind.Feature && e.From.Kind is NodeKind.Record or NodeKind.Decision))
        {
            if (!_featuresOf.TryGetValue(edge.From.Value, out var list))
            {
                _featuresOf[edge.From.Value] = list = [];
            }

            list.Add(edge.To.Value);
        }
    }

    internal IReadOnlyList<string> FeatureKeys => [.. _nodes.Values.Where(n => n.Key.Kind == NodeKind.Feature).Select(n => n.Key.Value).Order(StringComparer.Ordinal)];

    internal IEnumerable<GraphNode> Intent => _nodes.Values.Where(n => n.Key.Kind is NodeKind.Record or NodeKind.Decision).OrderBy(n => n.Key.Value, StringComparer.Ordinal);

    internal GraphNode? Node(string key) => _nodes.GetValueOrDefault(key);

    internal string Label(string key) => _nodes.TryGetValue(key, out var node) && !string.IsNullOrWhiteSpace(node.Label) ? node.Label : key[(key.IndexOf(':', StringComparison.Ordinal) + 1)..];

    internal IReadOnlyList<string> FeaturesOf(string intentKey) => _featuresOf.GetValueOrDefault(intentKey) ?? [];

    // The titles of the records and decisions that mention or decide any of the features.
    internal IReadOnlyList<string> RecordTitles(IReadOnlyList<string> features)
    {
        var set = features.ToHashSet(StringComparer.Ordinal);
        return [.. Intent.Where(n => FeaturesOf(n.Key.Value).Any(set.Contains)).Select(n => n.Label).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
    }

    // The record a node stands for, when its key is one ('record:linear:issue/1' cites 'linear:issue/1').
    internal static EvidenceRef? Cites(GraphNode intent) =>
        intent.Key.Kind == NodeKind.Record && RecordKey.TryFrom(intent.Key.Name, out var record) ? new EvidenceRef(record) : null;
}
