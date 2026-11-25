using Aw.Context.Core.Graph;

namespace Aw.Context.Tests.Graph;

/// <summary>The precomputed set answers like the per-node edge scan (#4363).</summary>
[Trait("Lane", "unit")]
public sealed class PageKindsComponentSetTests
{
    private static GraphNode Node(string key) => new(NodeKey.From(key), key);

    private static GraphEdge Implements(string code, string target) => new(NodeKey.From(code), NodeKey.From(target), EdgeType.Implements);

    [Fact]
    public void The_set_form_agrees_with_the_edge_scan_for_every_node()
    {
        GraphNode[] nodes =
        [
            Node("surface:screen/Home"), Node("surface:dialog/Confirm"), Node("surface:pane/Side"), Node("surface:api/Orders"),
            Node("surface:route/GET:/a"), Node("surface:route/GET:/b"), Node("surface:route/GET:/c"), Node("surface:route/GET:/none"), Node("feature:f"),
        ];
        GraphEdge[] edges =
        [
            Implements("code:web/A.tsx", "surface:route/GET:/a"),
            Implements("code:web/B.JSX", "surface:route/GET:/b"),
            Implements("code:api/C.cs", "surface:route/GET:/c"),
            Implements("code:web/F.tsx", "feature:f"),
        ];
        var set = PageKinds.ComponentImplemented(edges);

        foreach (var node in nodes)
        {
            Assert.Equal(PageKinds.IsPage(node, edges), PageKinds.IsPageIn(node, set));
        }

        Assert.True(PageKinds.IsPageIn(nodes[4], set));
        Assert.False(PageKinds.IsPageIn(nodes[6], set));
    }
}
