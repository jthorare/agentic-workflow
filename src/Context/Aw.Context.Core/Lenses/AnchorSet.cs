using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Kernel;

namespace Aw.Context.Core.Lenses;

/// <summary>The anchor set several lens answers share (scoping spec C1): which given anchors resolve to scopes, which don't, and the coverage over their union.</summary>
internal static class AnchorSet
{
    /// <summary>The anchors that resolve (de-duplicated by node) and the values that don't (as given). An ambiguous name refuses the call; with nothing resolved the first miss is the answer.</summary>
    internal static Result<(List<LensScope> Scopes, List<string> Missing)> Resolve(GraphSnapshot graph, IReadOnlyList<string> given, int depth, CascadeOptions? options = null)
    {
        var scopes = new List<LensScope>();
        var nodes = LensScope.NodesOf(graph);
        var missing = new List<string>();
        Error? first = null;
        foreach (var raw in given)
        {
            var node = AnchorMatch.Resolve(graph, raw);
            if (node.IsSuccess)
            {
                if (!scopes.Exists(s => s.Anchor.Key.Value == node.Value.Key.Value) && LensScope.Of(graph, node.Value.Key, depth, options, nodes) is { } scope)
                {
                    scopes.Add(scope);
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

        return scopes.Count == 0 && first is { } none ? none : (scopes, missing);
    }

    /// <summary>The coverage over the union of the scopes' own scopes, with each anchor's own and the map's.</summary>
    internal static LensCoverage Coverage(LensBasis basis, GraphSnapshot graph, List<LensScope> scopes, IReadOnlySet<string> ownUnion)
    {
        var byAnchor = scopes.ToDictionary(s => s.Anchor.Key.Value, LensCoverageBuilder.Of, StringComparer.Ordinal);
        return LensCoverageBuilder.Of(scopes[0].Graph, scopes[0].Nodes, [.. ownUnion.Select(NodeKey.From)]) with { Map = MapCoverageBuilder.Of(graph, ownUnion, basis), ByAnchor = byAnchor };
    }
}
