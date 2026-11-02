namespace Aw.Context.Core.Graph;

/// <summary>
/// Which repo each node of a graph belongs to, for a page that lists a map's repos and shows one repo's part of it. A node's repo is the repo its own evidence names ('repo:&lt;repo&gt;/&lt;path&gt;'; for a code file the evidence of its
/// 'implements' and 'tests' edges counts too, and a record's own key does); failing that, the repo of the files that implement or test it, when they all name one; failing that, the repo the map was built for, except for
/// decisions and records, which belong to no repo unless their evidence says so. A node that evidence puts in several repos belongs to none (null).
/// </summary>
public static class NodeRepos
{
    private const string Scheme = "repo:";

    /// <summary>The repo of every node of <paramref name="graph"/>, by node key.</summary>
    /// <param name="graph">The graph.</param>
    /// <param name="mapRepo">The repo the map version was built for, or null when the caller has none.</param>
    /// <returns>Each node's repo, or null when it belongs to no single repo.</returns>
    public static IReadOnlyDictionary<string, string?> Index(GraphSnapshot graph, string? mapRepo)
    {
        ArgumentNullException.ThrowIfNull(graph);
        var links = graph.Edges.Where(e => e.Type is EdgeType.Implements or EdgeType.Tests or EdgeType.Tags).ToList();
        var direct = DirectRepos(graph, links);
        var derived = DerivedRepos(direct, links);
        var index = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var node in graph.Nodes)
        {
            var key = node.Key.Value;
            var repos = direct[key];
            if (repos.Count == 0 && derived.TryGetValue(key, out var reached))
            {
                repos = reached;
            }

            index[key] = repos.Count switch
            {
                1 => repos.First(),
                0 when mapRepo is not null && node.Key.Kind is not (NodeKind.Decision or NodeKind.Record) => mapRepo,
                _ => null,
            };
        }

        return index;
    }

    // Each node's own repos: its evidence, a record's key, and the evidence of its 'implements' and 'tests' edges.
    private static Dictionary<string, HashSet<string>> DirectRepos(GraphSnapshot graph, List<GraphEdge> links)
    {
        var direct = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var node in graph.Nodes)
        {
            direct[node.Key.Value] = Direct(node);
        }

        foreach (var edge in links.Where(e => direct.ContainsKey(e.From.Value)))
        {
            direct[edge.From.Value].UnionWith(Named(edge.EvidenceOrEmpty));
        }

        return direct;
    }

    // The repos a node reaches through the files that implement or test it.
    private static Dictionary<string, HashSet<string>> DerivedRepos(Dictionary<string, HashSet<string>> direct, List<GraphEdge> links)
    {
        var derived = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var edge in links)
        {
            if (!direct.TryGetValue(edge.From.Value, out var from) || from.Count == 0)
            {
                continue;
            }

            if (!derived.TryGetValue(edge.To.Value, out var into))
            {
                derived[edge.To.Value] = into = new HashSet<string>(StringComparer.Ordinal);
            }

            into.UnionWith(from);
        }

        return derived;
    }

    private static HashSet<string> Direct(GraphNode node)
    {
        var found = Named(node.EvidenceOrEmpty);
        if (node.Key.Kind == NodeKind.Record && RepoOf(node.Key.Name) is { } own)
        {
            _ = found.Add(own);
        }

        return found;
    }

    private static HashSet<string> Named(IEnumerable<EvidenceRef> evidence) =>
        [.. evidence.Select(e => RepoOf(e.Key.Value)).OfType<string>()];

    // 'repo:<repo>/<path>' names <repo>.
    private static string? RepoOf(string key)
    {
        if (!key.StartsWith(Scheme, StringComparison.Ordinal))
        {
            return null;
        }

        var slash = key.IndexOf('/', Scheme.Length);
        return slash > Scheme.Length ? key[Scheme.Length..slash] : null;
    }
}
