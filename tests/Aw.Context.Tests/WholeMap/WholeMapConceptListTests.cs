using System.Globalization;
using Aw.Context.Core.Graph;
using Aw.Context.Core.WholeMap;
using Aw.Context.Tests.Concepts;

namespace Aw.Context.Tests.WholeMap;

/// <summary>An opened concept's member list (#3674): the members and the code behind them, ranked, 50 a page, in tabs that add up to the card's count.</summary>
public sealed class WholeMapConceptListTests
{
    private static readonly WholeMapBasis _basis = new("v7", MapCommit: null, Draft: false, DateTimeOffset.UnixEpoch, Repo: null);

    private static Kernel.Result<WholeMapView> Ask(ConceptGraph world, string tab = "all", int page = 1, string concept = "concept:scheduling")
    {
        var graph = world.Build();
        var ask = new WholeMapAsk(_basis, WholeMapDetail.Auto, [], Area: null) { Concept = concept, ConceptTab = tab, Page = page };
        return WholeMapBuilder.Build(ask, graph, MapAreas.Assign(graph), WholeMapRules.Default, ConceptGraph.Text);
    }

    // One concept with 120 features, each implemented by its own code file: 240 items. Feature 119 has two code files, so it ranks first.
    private static ConceptGraph Big()
    {
        var world = new ConceptGraph().Concept("concept:big", "Big", "Lots.", "derived");
        for (var i = 0; i < 120; i++)
        {
            var feature = string.Create(CultureInfo.InvariantCulture, $"feature:f{i:000}");
            var code = string.Create(CultureInfo.InvariantCulture, $"code:src/F{i:000}.cs");
            _ = world.Node(feature, string.Create(CultureInfo.InvariantCulture, $"Feature {i:000}")).Node(code).Member(feature, "concept:big").Edge(code, EdgeType.Implements, feature);
        }

        return world.Node("code:src/Extra.cs").Edge("code:src/Extra.cs", EdgeType.Implements, "feature:f119");
    }

    [Fact, Trait("Lane", "unit")]
    public void The_all_tab_lists_the_members_and_the_code_behind_them_and_the_tab_counts_add_up()
    {
        var view = Ask(ConceptGraph.Product()).Value;

        Assert.Equal(["code:src/Shared.cs", "code:src/Shifts.cs", "feature:shifts", "feature:swaps", "record:swap-rules"], view.Nodes!.Select(n => n.Key.Value).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Equal(("all", 1, 1, 5, 2, 2, 1), (view.ConceptList!.Tab, view.ConceptList.Page, view.ConceptList.Pages, view.ConceptList.Items, view.ConceptList.Code, view.ConceptList.Features, view.ConceptList.Records));
        Assert.Equal(view.ConceptList.Code + view.ConceptList.Features + view.ConceptList.Records, view.ConceptList.Items);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("code", 2)]
    [InlineData("feature", 2)]
    [InlineData("record", 1)]
    public void A_tab_lists_only_its_kind_and_says_how_many_it_holds(string tab, int count)
    {
        var view = Ask(ConceptGraph.Product(), tab).Value;

        Assert.Equal(count, view.Nodes!.Count);
        Assert.Equal((tab, count), (view.ConceptList!.Tab, view.ConceptList.Items));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_big_concept_answers_fifty_a_page_in_rank_order_with_the_most_linked_first()
    {
        var first = Ask(Big(), concept: "concept:big").Value;
        var last = Ask(Big(), page: 5, concept: "concept:big").Value;

        Assert.Equal(50, first.Nodes!.Count);
        Assert.Equal((1, 5, 241), (first.ConceptList!.Page, first.ConceptList.Pages, first.ConceptList.Items));
        Assert.Equal(41, last.Nodes!.Count);
        Assert.Equal("feature:f119", first.Nodes![0].Key.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_pages_together_list_every_item_once_and_a_page_past_the_last_is_the_last()
    {
        var keys = Enumerable.Range(1, 5).SelectMany(p => Ask(Big(), page: p, concept: "concept:big").Value.Nodes!.Select(n => n.Key.Value)).ToList();

        Assert.Equal(241, keys.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(241, keys.Count);
        Assert.Equal(5, Ask(Big(), page: 99, concept: "concept:big").Value.ConceptList!.Page);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_list_holds_the_edges_among_its_own_page_and_never_the_concept_node()
    {
        var view = Ask(ConceptGraph.Product()).Value;

        Assert.DoesNotContain(view.Nodes!, n => n.Key.Kind == NodeKind.Concept);
        Assert.All(view.Edges!, e => Assert.Contains(view.Nodes!, n => n.Key.Value == e.From.Value));
        Assert.All(view.Edges!, e => Assert.Contains(view.Nodes!, n => n.Key.Value == e.To.Value));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_concept_with_nothing_in_it_is_an_empty_list_and_an_unknown_tab_reads_as_all()
    {
        var empty = Ask(new ConceptGraph().Concept("concept:none", "None", "Nothing.", "derived"), concept: "concept:none").Value;
        var unknown = Ask(ConceptGraph.Product(), "bogus").Value;

        Assert.Equal((0, 1, 0), (empty.Nodes!.Count, empty.ConceptList!.Pages, empty.ConceptList.Items));
        Assert.Equal(("all", 5), (unknown.ConceptList!.Tab, unknown.ConceptList.Items));
    }
}
