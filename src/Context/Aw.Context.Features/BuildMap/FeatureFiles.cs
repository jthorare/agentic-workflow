using Aw.Context.Core.Graph;

namespace Aw.Context.Features.BuildMap;

/// <summary>A feature, its label and the code files that implement it.</summary>
/// <param name="Feature">The feature's key.</param>
/// <param name="Label">Its label.</param>
/// <param name="Paths">The repo-relative paths of its implementing files, in order.</param>
internal sealed record FeatureFiles(NodeKey Feature, string Label, IReadOnlyList<string> Paths)
{
    /// <summary>The features of a graph with the files its 'implements' edges name, in key order.</summary>
    internal static List<FeatureFiles> Of(GraphSnapshot graph)
    {
        var paths = graph.Edges
            .Where(e => e.Type == EdgeType.Implements && e.From.Kind == NodeKind.Code && e.To.Kind == NodeKind.Feature)
            .GroupBy(e => e.To.Value, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)[.. g.Select(e => e.From.Name).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)], StringComparer.Ordinal);
        return [.. graph.Nodes
            .Where(n => n.Key.Kind == NodeKind.Feature)
            .GroupBy(n => n.Key.Value, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new FeatureFiles(g.First().Key, g.First().Label, paths.GetValueOrDefault(g.Key) ?? []))];
    }
}
