using System.Globalization;
using Aw.Context.Core.Graph;
using Aw.Context.Core.WholeMap;
using Aw.Context.Tests.Concepts;

namespace Aw.Context.Tests.WholeMap;

/// <summary>The drill-in's engine side (#3674): an opened area is ranked and paged, 50 a page, and an opened concept counts only what it opens.</summary>
public sealed class WholeMapDrillInTests
{
    private static readonly WholeMapBasis _basis = new("v7", MapCommit: null, Draft: false, DateTimeOffset.UnixEpoch, Repo: null);

    private static readonly int[] _pages = [1, 2, 3];

    // One feature with 120 screens under it (121 nodes). s119 depends on s000 and s001, so it has three links where the other screens have one.
    private static ConceptGraph Big()
    {
        var world = new ConceptGraph().Node("feature:big", "Big");
        for (var i = 0; i < 120; i++)
        {
            var key = string.Create(CultureInfo.InvariantCulture, $"surface:screen/s{i:000}");
            _ = world.Node(key, string.Create(CultureInfo.InvariantCulture, $"Screen {i:000}")).Edge(key, EdgeType.PartOf, "feature:big");
        }

        return world.Edge("surface:screen/s119", EdgeType.DependsOn, "surface:screen/s000").Edge("surface:screen/s119", EdgeType.DependsOn, "surface:screen/s001");
    }

    private static Kernel.Result<WholeMapView> Ask(ConceptGraph world, int page = 1, string? area = "feature:big", string? concept = null)
    {
        var graph = world.Build();
        var ask = new WholeMapAsk(_basis, WholeMapDetail.Auto, [], area) { Concept = concept, Page = page };
        return WholeMapBuilder.Build(ask, graph, MapAreas.Assign(graph), WholeMapRules.Default, ConceptGraph.Text);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_open_area_answers_one_page_of_fifty_and_says_how_many_pages_there_are()
    {
        var view = Ask(Big()).Value;

        Assert.Equal(50, view.Nodes!.Count);
        Assert.Equal((true, 121, 1, 3), (view.Area!.Truncated, view.Area.Counts.Nodes, view.Area.Page, view.Area.Pages));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(1, 50)]
    [InlineData(2, 50)]
    [InlineData(3, 21)]
    public void Each_page_holds_the_next_slice_and_the_last_holds_the_rest(int page, int count)
    {
        var view = Ask(Big(), page).Value;

        Assert.Equal(count, view.Nodes!.Count);
        Assert.Equal(page, view.Area!.Page);
        Assert.Equal(page < 3, view.Area.Truncated);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_pages_together_list_every_node_once()
    {
        var keys = _pages.SelectMany(p => Ask(Big(), p).Value.Nodes!.Select(n => n.Key.Value)).ToList();

        Assert.Equal(121, keys.Count);
        Assert.Equal(121, keys.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact, Trait("Lane", "unit")]
    public void Nodes_with_more_links_come_first_then_kind_and_key()
    {
        var view = Ask(Big()).Value;

        Assert.Equal(["feature:big", "surface:screen/s119", "surface:screen/s000", "surface:screen/s001", "surface:screen/s002"], view.Nodes!.Take(5).Select(n => n.Key.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_edges_of_a_page_are_the_ones_between_the_nodes_on_it()
    {
        var view = Ask(Big(), page: 2).Value;

        var keys = view.Nodes!.Select(n => n.Key.Value).ToHashSet(StringComparer.Ordinal);
        Assert.All(view.Edges!, e => Assert.True(keys.Contains(e.From.Value) && keys.Contains(e.To.Value)));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_page_past_the_last_is_the_last_page()
    {
        var view = Ask(Big(), page: 9).Value;

        Assert.Equal((3, 21), (view.Area!.Page, view.Nodes!.Count));
    }

    [Fact, Trait("Lane", "unit")]
    public void An_opened_concept_counts_the_nodes_and_links_it_opens_not_the_whole_map()
    {
        var view = Ask(ConceptGraph.Product(), area: null, concept: "concept:scheduling").Value;

        Assert.Equal((view.Nodes!.Count, view.Edges!.Count), (view.Counts.Nodes, view.Counts.Edges));
        Assert.Equal(8, view.Counts.Nodes);
        Assert.Equal(view.Nodes.Count(n => n.Kind == NodeKind.Code), view.Counts.ByKind["code"]);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_opened_concepts_areas_count_only_the_members_it_opens()
    {
        var view = Ask(ConceptGraph.Product(), area: null, concept: "concept:scheduling").Value;

        Assert.Equal(view.Nodes!.Count(n => n.Kind != NodeKind.Concept), view.Areas.Sum(a => a.Members));
    }
}
