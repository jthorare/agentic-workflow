using System.Globalization;
using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Map;

/// <summary>The graph checks of a map file, over one index of its keys (linear in the file's size).</summary>
internal static class MapGraphChecks
{
    /// <summary>Checks nodes, the file's edges and aliases; returns the file's edges resolved and deduplicated.</summary>
    public static List<GraphEdge> Run(GraphSnapshot graph, MapProblems problems)
    {
        var nodes = Nodes(graph, problems);
        var aliases = Aliases(graph, nodes, problems);
        return Edges(graph, nodes, aliases, problems);
    }

    private static Dictionary<string, GraphNode> Nodes(GraphSnapshot graph, MapProblems problems)
    {
        var index = new Dictionary<string, GraphNode>(StringComparer.Ordinal);
        for (var i = 0; i < graph.Nodes.Count; i++)
        {
            var node = graph.Nodes[i];
            var path = Fmt($"nodes[{i}]");
            if (!index.TryAdd(node.Key.Value, node))
            {
                problems.Invalid($"{path}.key", $"'{node.Key.Value}' is defined twice (or a decision in the file has the same key)");
            }

            if (!NodeKinds.HasValidSurfaceKind(node.Key))
            {
                problems.Invalid($"{path}.key", $"'{node.Key.Value}' needs a surface kind ({string.Join(", ", NodeKinds.SurfaceKinds.Order(StringComparer.Ordinal))}) and a name: 'surface:<kind>/<name>'");
            }

            if (string.IsNullOrWhiteSpace(node.Label))
            {
                problems.Invalid($"{path}.label", "a label is required");
            }

            Confidence(node.Confidence, $"{path}.confidence", problems);
        }

        return index;
    }

    private static Dictionary<string, string> Aliases(GraphSnapshot graph, Dictionary<string, GraphNode> nodes, MapProblems problems)
    {
        var index = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < graph.Aliases.Count; i++)
        {
            var alias = graph.Aliases[i];
            var path = Fmt($"aliases[{i}]");
            if (nodes.ContainsKey(alias.Alias.Value))
            {
                problems.Invalid($"{path}.alias", $"'{alias.Alias.Value}' is a node's own key");
            }
            else if (!index.TryAdd(alias.Alias.Value, alias.Target.Value))
            {
                problems.Invalid($"{path}.alias", $"'{alias.Alias.Value}' is defined twice");
            }

            if (!nodes.ContainsKey(alias.Target.Value))
            {
                problems.Undefined($"{path}.target", alias.Target.Value);
            }
        }

        return index;
    }

    private static List<GraphEdge> Edges(GraphSnapshot graph, Dictionary<string, GraphNode> nodes, Dictionary<string, string> aliases, MapProblems problems)
    {
        var edges = new List<GraphEdge>();
        var seen = new HashSet<(string, string, EdgeType)>();
        for (var i = 0; i < graph.Edges.Count; i++)
        {
            var edge = graph.Edges[i];
            var path = Fmt($"edges[{i}]");
            var from = Resolve(edge.From, nodes, aliases, $"{path}.from", problems);
            var to = Resolve(edge.To, nodes, aliases, $"{path}.to", problems);
            Confidence(edge.Confidence, $"{path}.confidence", problems);
            if (from is null || to is null || Refused(edge, from, to, path, problems) || CountRefused(edge, from, path, problems))
            {
                continue;
            }

            if (seen.Add((from.Key.Value, to.Key.Value, edge.Type)))
            {
                edges.Add(edge with { From = from.Key, To = to.Key });
            }
        }

        return edges;
    }

    private static bool Refused(GraphEdge edge, GraphNode from, GraphNode to, string path, MapProblems problems)
    {
        var problem = edge.Type == EdgeType.Quotes ? "'quotes' edges come from the file's decisions; drop this one"
            : from.Key.Value == to.Key.Value ? $"'{from.Key.Value}' can't be joined to itself"
            : !EdgeRules.Allows(edge.Type, from.Key.Kind, to.Key.Kind) ? $"'{EdgeRules.Name(edge.Type)}' doesn't go from a {Scheme(from.Key)} to a {Scheme(to.Key)} (spec §6.1's edge table)"
            : null;
        if (problem is not null)
        {
            problems.Invalid($"{path}.type", problem);
        }

        return problem is not null;
    }

    // A count belongs to a roll-up edge only: a feature's deterministic 'depends-on', 'reads' or 'writes' that the build made from its files (spec 2026-10-27 §3), at least 1.
    private static bool CountRefused(GraphEdge edge, GraphNode from, string path, MapProblems problems)
    {
        if (edge.Count is not { } count)
        {
            return false;
        }

        var rollUp = from.Key.Kind == NodeKind.Feature && edge.Type is EdgeType.DependsOn or EdgeType.Reads or EdgeType.Writes && edge.Provenance == Results.Provenance.Deterministic;
        var problem = !rollUp ? "a count belongs to a roll-up edge only (a feature's deterministic 'depends-on', 'reads' or 'writes')" : count < 1 ? Fmt($"{count} isn't a count (1 or more)") : null;
        if (problem is not null)
        {
            problems.Invalid($"{path}.count", problem);
        }

        return problem is not null;
    }

    private static GraphNode? Resolve(NodeKey key, Dictionary<string, GraphNode> nodes, Dictionary<string, string> aliases, string path, MapProblems problems)
    {
        var target = aliases.TryGetValue(key.Value, out var aliased) ? aliased : key.Value;
        if (nodes.TryGetValue(target, out var node))
        {
            return node;
        }

        problems.Undefined(path, key.Value);
        return null;
    }

    private static void Confidence(double value, string path, MapProblems problems)
    {
        if (value is < 0 or > 1 || double.IsNaN(value))
        {
            problems.Invalid(path, Fmt($"{value} is outside 0 to 1"));
        }
    }

    private static string Scheme(NodeKey key) => key.Value[..key.Value.IndexOf(':', StringComparison.Ordinal)];

    private static string Fmt(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
