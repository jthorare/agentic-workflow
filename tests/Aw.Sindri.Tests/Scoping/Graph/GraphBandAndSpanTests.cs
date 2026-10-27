using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Context.Core.ScopeGraph;
using Aw.Kernel.Testing;
using Aw.Sindri.Features.Scoping;

namespace Aw.Sindri.Tests.Scoping.Graph;

/// <summary>Issue 1005: a record or decision node sits in the intent band, and a citation with no span leaves the key out of its JSON.</summary>
[Lane(TestLane.Unit)]
public sealed class GraphBandAndSpanTests
{
    [Theory]
    [InlineData("record")]
    [InlineData("decision")]
    public void The_lenses_fallback_puts_a_record_or_decision_node_in_the_intent_band(string kind)
    {
        var lenses = JsonNode.Parse($$$"""{"related":{"mapVersion":"v1","depth":2,"hops":[],"nodes":[],"edges":[],"items":[{"lensItem":"L-related-1","kind":"{{{kind}}}","label":"Expiry decision","citations":["R-9"]}]},"code":{"nodes":[],"edges":[],"items":[]},"product":{"nodes":[],"edges":[],"items":[]},"history":{"nodes":[],"edges":[],"items":[]}}""")!.AsObject();

        var graph = GraphGather.Assemble(graphView: null, lenses, anchors: []);

        Assert.Equal("intent", ScopeJson.Str(Assert.Single(ScopeJson.Items(graph, "nodes")), "band"));
    }

    [Fact]
    public void A_citation_with_no_span_serializes_without_a_span_key_and_one_with_a_span_keeps_it()
    {
        Assert.Equal("""{"Key":"repo:web/a.ts"}""", JsonSerializer.Serialize(new ScopeGraphCitation("repo:web/a.ts")));
        Assert.Equal("""{"Key":"repo:web/a.ts","Span":"L1-L9"}""", JsonSerializer.Serialize(new ScopeGraphCitation("repo:web/a.ts", "L1-L9")));
    }
}
