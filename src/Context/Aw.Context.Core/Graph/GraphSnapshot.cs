namespace Aw.Context.Core.Graph;

/// <summary>One map version's graph in memory: nodes, edges, aliases and decisions (S-3b adds the query algorithms over it).</summary>
/// <param name="Nodes">The nodes.</param>
/// <param name="Edges">The edges.</param>
/// <param name="Aliases">The aliases.</param>
/// <param name="Decisions">The decisions.</param>
public sealed record GraphSnapshot(
    IReadOnlyList<GraphNode> Nodes,
    IReadOnlyList<GraphEdge> Edges,
    IReadOnlyList<NodeAlias> Aliases,
    IReadOnlyList<DecisionRow> Decisions)
{
    /// <summary>A graph with nothing in it.</summary>
    public static GraphSnapshot Empty { get; } = new([], [], [], []);

    /// <summary>The node a key or alias names.</summary>
    /// <param name="key">A node key or an alias.</param>
    /// <returns>The node, or null when neither names one.</returns>
    public GraphNode? Resolve(NodeKey key)
    {
        var target = Aliases.FirstOrDefault(a => a.Alias.Value == key.Value)?.Target ?? key;
        return Nodes.FirstOrDefault(n => n.Key.Value == target.Value);
    }

    /// <summary>
    /// What a session scoped to <paramref name="repo"/> may see (spec §3): <see cref="RepoScope"/>'s nodes with their part-of hierarchy (<see cref="RepoScope.WithHierarchy"/>), the records and decisions linked to them (owner decision 2026-10-15, B),
    /// the concepts those belong to (<see cref="RepoScope.WithConcepts"/>), and the edges, aliases and decisions among those. A link to a node outside the scope is dropped with that node.
    /// </summary>
    /// <param name="repo">The session's X-Sindri-Repo.</param>
    /// <returns>The scoped graph (empty when the repo implements nothing).</returns>
    public GraphSnapshot ScopedTo(Engine.ContextRepoName repo)
    {
        var keys = RepoScope.WithConcepts(this, RepoScope.WithLinkedIntent(this, RepoScope.WithHierarchy(this, RepoScope.NodesFor(this, repo))));
        return new GraphSnapshot(
            [.. Nodes.Where(n => keys.Contains(n.Key.Value))],
            [.. Edges.Where(e => keys.Contains(e.From.Value) && keys.Contains(e.To.Value))],
            [.. Aliases.Where(a => keys.Contains(a.Target.Value))],
            [.. Decisions.Where(d => keys.Contains(d.ToKey().Value))]);
    }
}
