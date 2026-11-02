using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Walks;
using Aw.Kernel;

namespace Aw.Context.Core.Lenses;

/// <summary>The anchor set several lens answers share (scoping spec C1): which given anchors resolve to scopes, which don't, and the coverage over their union.</summary>
internal static class AnchorSet
{
    /// <summary>
    /// The anchors that resolve (de-duplicated by node) and the values that don't (as given). An ambiguous name refuses the call; with nothing resolved the first miss is the answer.
    /// The names are resolved one after another; each anchor's scope (its cascade) is then built over <paramref name="walk"/>'s cores and the scopes come back in the order the anchors were given.
    /// </summary>
    internal static Result<(List<LensScope> Scopes, List<string> Missing)> Resolve(GraphSnapshot graph, IReadOnlyList<string> given, int depth, CascadeOptions? options, WalkOptions walk)
    {
        var picked = new List<GraphNode>();
        var missing = new List<string>();
        Error? first = null;
        foreach (var raw in given)
        {
            var node = AnchorMatch.Resolve(graph, raw);
            if (node.IsSuccess)
            {
                if (!picked.Exists(n => n.Key.Value == node.Value.Key.Value))
                {
                    picked.Add(node.Value);
                }

                continue;
            }

            if (node.Error.Code == ContextErrors.AmbiguousAnchor.Code)
            {
                return node.Error;
            }

            missing.Add(raw);
            first ??= node.Error;
        }

        var scopes = ScopesOf(graph, picked, depth, options, walk);
        return scopes.Count == 0 && first is { } none ? none : (scopes, missing);
    }

    // The graph's lookups are built once, here, before any worker asks for them: a lookup built by two workers at once is built twice.
    private static List<LensScope> ScopesOf(GraphSnapshot graph, List<GraphNode> picked, int depth, CascadeOptions? options, WalkOptions walk)
    {
        var nodes = LensScope.NodesOf(graph);
        if (walk.Degree > 1 && picked.Count > 1)
        {
            _ = GraphIndex.For(graph);
        }

        var built = BoundedParallel.MapSync(picked, node => LensScope.Of(graph, node.Key, depth, options, nodes), walk.Degree);
        var scopes = new List<LensScope>();
        for (var i = 0; i < built.Length; i++)
        {
            if (built[i] is { } scope && !scopes.Exists(s => s.Anchor.Key.Value == picked[i].Key.Value))
            {
                scopes.Add(scope);
            }
        }

        return scopes;
    }

    /// <summary>The coverage over the union of the scopes' own scopes, with each anchor's own and the map's.</summary>
    internal static LensCoverage Coverage(LensBasis basis, GraphSnapshot graph, List<LensScope> scopes, IReadOnlySet<string> ownUnion, WalkOptions walk)
    {
        var counted = BoundedParallel.MapSync(scopes, LensCoverageBuilder.Of, walk.Degree);
        var byAnchor = scopes.Select((s, i) => (Key: s.Anchor.Key.Value, Coverage: counted[i])).ToDictionary(c => c.Key, c => c.Coverage, StringComparer.Ordinal);
        return LensCoverageBuilder.Of(scopes[0].Graph, scopes[0].Nodes, [.. ownUnion.Select(NodeKey.From)]) with { Map = MapCoverageBuilder.Of(graph, ownUnion, basis), ByAnchor = byAnchor };
    }
}
