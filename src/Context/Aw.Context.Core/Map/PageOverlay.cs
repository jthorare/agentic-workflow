using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Map;

/// <summary>The owner's named pages laid over the map at read time (pages spec §3.3): nothing is stored, so an approved edit shows on the next read and a rebuild loses nothing.</summary>
public static class PageOverlay
{
    private const string Scheme = "page:";
    private const string ConceptPrefix = "concept:";

    /// <summary>Lays the profile's pages over a graph: relabels their surfaces and actions (or adds owner nodes) and links each action to its concepts, every touched node and edge carrying 'page:&lt;repo&gt;/&lt;page-id&gt;' evidence. Never removes a node.</summary>
    /// <param name="graph">The graph after the deny cut.</param>
    /// <param name="repos">The profile's repos, each with its pages.</param>
    /// <returns>The graph with the pages; the same graph when there are none. Applying it twice gives the same graph.</returns>
    public static GraphSnapshot Apply(GraphSnapshot graph, IReadOnlyList<ContextRepo> repos)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(repos);
        var pages = repos.SelectMany(r => r.Pages).DistinctBy(p => p.Id).Take(PageRules.MaxPages).ToList();
        if (pages.Count == 0)
        {
            return graph;
        }

        var state = new Overlay(graph, repos.Count == 1 ? repos[0].Name : null);
        foreach (var page in pages)
        {
            state.Lay(page);
        }

        return state.Build();
    }

    /// <summary>The graph without any node or edge that carries a 'page:' evidence ref: what an export writes and an import keeps.</summary>
    /// <param name="graph">The graph.</param>
    /// <returns>The graph without the overlay; the same graph when it has none.</returns>
    public static GraphSnapshot Without(GraphSnapshot graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        var dropped = graph.Nodes.Where(n => HasPageRef(n.EvidenceOrEmpty)).Select(n => n.Key.Value).ToHashSet(StringComparer.Ordinal);
        var edges = graph.Edges.Where(e => !HasPageRef(e.EvidenceOrEmpty) && !dropped.Contains(e.From.Value) && !dropped.Contains(e.To.Value)).ToList();
        if (dropped.Count == 0 && edges.Count == graph.Edges.Count)
        {
            return graph;
        }

        return graph with
        {
            Nodes = [.. graph.Nodes.Where(n => !dropped.Contains(n.Key.Value))],
            Edges = edges,
            Aliases = [.. graph.Aliases.Where(a => !dropped.Contains(a.Target.Value))],
        };
    }

    private static bool HasPageRef(IReadOnlyList<EvidenceRef> evidence) => evidence.Any(r => r.Key.Value.StartsWith(Scheme, StringComparison.Ordinal));

    private static string? RepoOf(EvidenceRef reference)
    {
        const string prefix = "repo:";
        var value = reference.Key.Value;
        if (!value.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var slash = value.IndexOf('/', StringComparison.Ordinal);
        return slash < 0 ? null : value[prefix.Length..slash];
    }

    private sealed record Target(string? Named, NodeKind Kind, string OwnerKey, string Label);

    private sealed class Overlay
    {
        private readonly List<GraphNode> _nodes;
        private readonly List<GraphEdge> _edges;
        private readonly GraphSnapshot _graph;
        private readonly ContextRepoName? _onlyRepo;
        private readonly Dictionary<string, int> _index = new(StringComparer.Ordinal);
        private readonly HashSet<(string, string, EdgeType)> _facts = [];

        public Overlay(GraphSnapshot graph, ContextRepoName? onlyRepo)
        {
            _graph = graph;
            _onlyRepo = onlyRepo;
            _nodes = [.. graph.Nodes];
            _edges = [.. graph.Edges];
            for (var i = 0; i < _nodes.Count; i++)
            {
                _ = _index.TryAdd(_nodes[i].Key.Value, i);
            }

            foreach (var e in _edges)
            {
                _ = _facts.Add((e.From.Value, e.To.Value, e.Type));
            }
        }

        public GraphSnapshot Build() => _graph with { Nodes = _nodes, Edges = _edges };

        public void Lay(PagePin page)
        {
            var repo = page.Repo ?? (page.SurfaceKey is { } k ? OnlyRepoOf(k) : null) ?? _onlyRepo;
            if (repo is not { } home)
            {
                return;
            }

            var mark = new EvidenceRef(RecordKey.From($"{Scheme}{home.Value}/{page.Id.Value}"));
            var (surfaceKey, _) = Name(new Target(page.SurfaceKey, NodeKind.Surface, $"surface:screen/owner-{page.Id.Value}", page.Name), home, mark);
            foreach (var action in page.Actions.Take(PageRules.MaxActions))
            {
                LayAction(action, (page, surfaceKey), home, mark);
            }
        }

        private void LayAction(PageActionPin action, (PagePin Page, string SurfaceKey) on, ContextRepoName home, EvidenceRef mark)
        {
            var (key, owner) = Name(new Target(action.ActionKey, NodeKind.Action, $"action:owner/{on.Page.Id.Value}/{action.Id.Value}", action.Name), home, mark);
            if (owner)
            {
                AddEdge(new GraphEdge(NodeKey.From(key), NodeKey.From(on.SurfaceKey), EdgeType.On, Provenance.Human, 1.0, [mark]));
            }
            else
            {
                _ = _edges.RemoveAll(e => e is { Type: EdgeType.Expresses, Provenance: Provenance.Deterministic } && e.From.Value == key);
            }

            foreach (var concept in action.Concepts.Take(PageRules.MaxConcepts))
            {
                var target = concept.StartsWith(ConceptPrefix, StringComparison.Ordinal) ? concept : ConceptPrefix + concept;
                if (_index.TryGetValue(target, out var at) && _nodes[at].Key.Kind == NodeKind.Concept)
                {
                    AddEdge(new GraphEdge(NodeKey.From(key), NodeKey.From(target), EdgeType.Expresses, Provenance.Human, 1.0, [mark]));
                }
            }
        }

        // The repo of a surface key, when its code is in one repo only.
        private ContextRepoName? OnlyRepoOf(string key)
        {
            var repos = ReposOf(key);
            return repos.Count == 1 && ContextRepoName.TryFrom(repos[0], out var name) ? name : null;
        }

        private List<string> ReposOf(string key) =>
            [.. _graph.Edges.Where(e => e.Type == EdgeType.Implements && e.To.Value == key).SelectMany(e => e.EvidenceOrEmpty).Select(RepoOf).OfType<string>().Distinct(StringComparer.Ordinal)];

        // Relabels the map node the owner named when this repo alone holds it; else makes (or finds) the owner node.
        private (string Key, bool Owner) Name(Target target, ContextRepoName home, EvidenceRef mark)
        {
            var (named, kind, ownerKey, label) = target;
            if (named is not null && _index.TryGetValue(named, out var at) && _nodes[at].Key.Kind == kind)
            {
                var repos = ReposOf(named);
                if (repos.TrueForAll(r => string.Equals(r, home.Value, StringComparison.Ordinal)))
                {
                    var node = _nodes[at];
                    var evidence = node.EvidenceOrEmpty.Any(r => r.Key.Value == mark.Key.Value) ? node.EvidenceOrEmpty : [.. node.EvidenceOrEmpty, mark];
                    _nodes[at] = node with { Label = label, Evidence = evidence };
                    return (named, false);
                }

                AddOwner(ownerKey, label, mark);
                CopyCode(named, ownerKey, home);
                return (ownerKey, true);
            }

            AddOwner(ownerKey, label, mark);
            return (ownerKey, true);
        }

        private void AddOwner(string key, string label, EvidenceRef mark)
        {
            if (_index.ContainsKey(key))
            {
                return;
            }

            _index[key] = _nodes.Count;
            _nodes.Add(new GraphNode(NodeKey.From(key), label, Provenance.Human, 1.0, [mark]));
        }

        // The 'implements' edges of the original whose evidence is in the page's repo, so the page still shows its code.
        private void CopyCode(string original, string owner, ContextRepoName home)
        {
            foreach (var e in _graph.Edges.Where(e => e.Type == EdgeType.Implements && e.To.Value == original).ToList())
            {
                var own = e.EvidenceOrEmpty.Where(r => string.Equals(RepoOf(r), home.Value, StringComparison.Ordinal)).ToList();
                if (own.Count > 0)
                {
                    AddEdge(e with { To = NodeKey.From(owner), Evidence = own });
                }
            }
        }

        private void AddEdge(GraphEdge edge)
        {
            if (_facts.Add((edge.From.Value, edge.To.Value, edge.Type)))
            {
                _edges.Add(edge);
            }
        }
    }
}
