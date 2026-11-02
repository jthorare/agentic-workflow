using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Features.Scoping;

namespace Aw.Sindri.Tests.Scoping.Graph;

/// <summary>Issue 1005: a graph node's citations are { key, span? } objects, the shape of the concept read, never path:line text.</summary>
[Lane(TestLane.Unit)]
public sealed class GraphCitationShapeTests
{
    [Fact]
    public void The_lenses_fallback_writes_each_citation_as_an_object_with_a_key()
    {
        var lenses = JsonNode.Parse("""{"code":{"mapVersion":"v1","depth":2,"hops":[],"nodes":[],"edges":[],"items":[{"lensItem":"L-code-2","kind":"commit","label":"fix b","citations":["git:abc123"]}]},"product":{"nodes":[],"edges":[],"items":[]},"related":{"nodes":[],"edges":[],"items":[]},"history":{"nodes":[],"edges":[],"items":[]}}""")!.AsObject();

        var graph = GraphGather.Assemble(graphView: null, lenses, anchors: []);

        var citation = Assert.Single(ScopeJson.Items(Assert.Single(ScopeJson.Items(graph, "nodes")), "citations"));
        Assert.Equal("git:abc123", ScopeJson.Str(citation, "key"));
    }

    [Fact]
    public void A_view_graph_keeps_its_citation_objects_and_a_concept_node_is_kept_as_it_is()
    {
        var view = JsonNode.Parse("""{"mapVersion":"v1","anchors":["concept:sharing"],"depth":1,"hops":[],"nodes":[{"id":"concept:sharing","kind":"concept","summary":"How notes are shared","rank":1,"lens":"product","lensItem":"L-product-1","citations":[{"key":"repo:web/docs/sharing.md","span":"L1-L9"}]}],"edges":[],"limits":[]}""")!.AsObject();

        var graph = GraphGather.Assemble(view, [], anchors: []);

        var node = Assert.Single(ScopeJson.Items(graph, "nodes"));
        Assert.Equal("concept", ScopeJson.Str(node, "kind"));
        Assert.Equal("L1-L9", ScopeJson.Str(Assert.Single(ScopeJson.Items(node, "citations")), "span"));
    }
}
