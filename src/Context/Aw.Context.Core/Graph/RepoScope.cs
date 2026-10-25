namespace Aw.Context.Core.Graph;

/// <summary>
/// The nodes a repo implements (spec §3): the targets of 'implements' edges with file evidence in that repo
/// ('repo:&lt;repo&gt;/&lt;path&gt;'), and the code files those edges start from ("that repo's files and code"). S-3b adds their cascade.
/// Since owner decision 2026-10-15 (B) a session also sees the intent linked to those nodes (<see cref="WithLinkedIntent"/>).
/// </summary>
public static class RepoScope
{
    /// <summary>The keys of the nodes <paramref name="repo"/> implements.</summary>
    /// <param name="graph">The graph.</param>
    /// <param name="repo">The repo's short name.</param>
    /// <returns>The node keys (ordinal), empty when it implements nothing.</returns>
    public static IReadOnlySet<string> NodesFor(GraphSnapshot graph, Engine.ContextRepoName repo)
    {
        ArgumentNullException.ThrowIfNull(graph);
        var prefix = $"repo:{repo.Value}/";
        return graph.Edges
            .Where(e => e.Type == EdgeType.Implements && e.EvidenceOrEmpty.Any(r => r.Key.Value.StartsWith(prefix, StringComparison.Ordinal)))
            .SelectMany(e => new[] { e.To.Value, e.From.Value })
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// <paramref name="implemented"/> with the part-of hierarchy around it: every part-of ancestor (the feature a person filed the repo's screen under,
    /// and that feature's own parents) and every part-of descendant of an implemented node (the actions and things on the repo's own screens). An
    /// ancestor brings only itself: its other children (another repo's screens under a shared feature) stay out unless this repo implements them too.
    /// </summary>
    /// <param name="graph">The graph.</param>
    /// <param name="implemented">The nodes the repo implements (<see cref="NodesFor"/>).</param>
    /// <returns>The keys of the implemented nodes, their part-of ancestors and the descendants of the implemented ones (ordinal).</returns>
    public static IReadOnlySet<string> WithHierarchy(GraphSnapshot graph, IReadOnlySet<string> implemented)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(implemented);
        var partOf = graph.Edges.Where(e => e.Type == EdgeType.PartOf).ToList();
        var parents = partOf.ToLookup(e => e.From.Value, e => e.To.Value, StringComparer.Ordinal);
        var children = partOf.ToLookup(e => e.To.Value, e => e.From.Value, StringComparer.Ordinal);
        var all = new HashSet<string>(implemented, StringComparer.Ordinal);
        all.UnionWith(Closure(implemented, parents));
        all.UnionWith(Closure(implemented, children));
        return all;
    }

    // Every key reachable from the start keys along next, the start keys left out.
    private static HashSet<string> Closure(IReadOnlySet<string> start, ILookup<string, string> next)
    {
        var reached = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>(start);
        while (pending.TryPop(out var key))
        {
            foreach (var other in next[key].Where(o => !start.Contains(o) && reached.Add(o)))
            {
                pending.Push(other);
            }
        }

        return reached;
    }

    /// <summary>
    /// <paramref name="scope"/> and the intent linked to it (spec §3 "intent records linked to those nodes"; owner decision 2026-10-15, B): the decisions with a 'decides' edge to a
    /// node in <paramref name="scope"/>, the records with a 'mentions' edge to one, and the records those decisions quote. A record or decision linked only to nodes outside
    /// <paramref name="scope"/> is not in the result. Nothing is added to an empty scope.
    /// </summary>
    /// <param name="graph">The graph.</param>
    /// <param name="scope">The nodes the repo implements (<see cref="NodesFor"/>).</param>
    /// <returns>The keys of the scope and of its linked records and decisions (ordinal).</returns>
    public static IReadOnlySet<string> WithLinkedIntent(GraphSnapshot graph, IReadOnlySet<string> scope)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(scope);
        if (scope.Count == 0)
        {
            return scope;
        }

        var decisions = graph.Edges.Where(e => e.Type == EdgeType.Decides && e.From.Kind == NodeKind.Decision && scope.Contains(e.To.Value)).Select(e => e.From.Value).ToHashSet(StringComparer.Ordinal);
        var records = graph.Edges.Where(e => e.Type == EdgeType.Mentions && e.From.Kind == NodeKind.Record && scope.Contains(e.To.Value)).Select(e => e.From.Value)
            .Concat(graph.Edges.Where(e => e.Type == EdgeType.Quotes && e.To.Kind == NodeKind.Record && decisions.Contains(e.From.Value)).Select(e => e.To.Value));
        var all = new HashSet<string>(scope, StringComparer.Ordinal);
        all.UnionWith(decisions);
        all.UnionWith(records);
        return all;
    }

    /// <summary>
    /// <paramref name="scope"/> and the concepts its members belong to: a concept node with a 'belongs-to' edge from a node in <paramref name="scope"/>. A concept with no member in scope stays out, so a session
    /// sees its repo's concepts, sized by its repo's members.
    /// </summary>
    /// <param name="graph">The graph.</param>
    /// <param name="scope">The nodes the repo implements with their hierarchy and linked intent.</param>
    /// <returns>The keys of the scope and of those concepts (ordinal).</returns>
    internal static IReadOnlySet<string> WithConcepts(GraphSnapshot graph, IReadOnlySet<string> scope)
    {
        var all = new HashSet<string>(scope, StringComparer.Ordinal);
        all.UnionWith(graph.Edges.Where(e => e.Type == EdgeType.BelongsTo && e.To.Kind == NodeKind.Concept && scope.Contains(e.From.Value)).Select(e => e.To.Value));
        return all;
    }
}
