using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Core.WholeMap;

namespace Aw.Context.Tests.WholeMap;

/// <summary>How <see cref="PagesInNoConcept"/> orders owner-named pages by the profile's rank (pages spec §3.4, #1791).</summary>
[Trait("Lane", "unit")]
public sealed class PageOrderTests
{
    private static GraphNode Page(string key, string? ownerRef = null) =>
        new(NodeKey.From(key), key, Provenance.Deterministic, 1.0, ownerRef is null ? null : [new EvidenceRef(RecordKey.From(ownerRef))]);

    private static GraphSnapshot Graph(params GraphNode[] nodes) => new(nodes, [], [], []);

    private static Dictionary<string, int> Rank(params (string Id, int Index)[] pages) => pages.ToDictionary(p => p.Id, p => p.Index, StringComparer.Ordinal);

    [Fact]
    public void Owner_pages_follow_the_rank_and_not_node_order()
    {
        var graph = Graph(Page("surface:screen/x", "page:web/second"), Page("surface:screen/y", "page:web/first"));

        var keys = PagesInNoConcept.Of(graph, Rank(("first", 0), ("second", 1)));

        Assert.Equal(["surface:screen/y", "surface:screen/x"], keys);
    }

    [Fact]
    public void An_owner_page_the_rank_does_not_know_comes_after_the_known_ones_by_key()
    {
        var graph = Graph(Page("surface:screen/b", "page:web/gone"), Page("surface:screen/a", "page:web/gone2"), Page("surface:screen/z", "page:web/known"));

        var keys = PagesInNoConcept.Of(graph, Rank(("known", 0)));

        Assert.Equal(["surface:screen/z", "surface:screen/a", "surface:screen/b"], keys);
    }

    [Fact]
    public void Suggested_pages_keep_their_order_after_the_owner_pages_and_an_empty_rank_keeps_node_order()
    {
        var graph = Graph(Page("surface:screen/s2"), Page("surface:screen/o2", "page:web/two"), Page("surface:screen/s1"), Page("surface:screen/o1", "page:web/one"));

        Assert.Equal(["surface:screen/o2", "surface:screen/o1", "surface:screen/s1", "surface:screen/s2"], PagesInNoConcept.Of(graph));
        Assert.Equal(["surface:screen/o1", "surface:screen/o2", "surface:screen/s1", "surface:screen/s2"], PagesInNoConcept.Of(graph, Rank(("one", 0), ("two", 1))));
    }
}
