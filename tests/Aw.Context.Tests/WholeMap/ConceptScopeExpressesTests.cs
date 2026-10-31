using System.Globalization;
using Aw.Context.Core.Graph;
using Aw.Context.Core.WholeMap;
using Aw.Context.Tests.Concepts;

namespace Aw.Context.Tests.WholeMap;

/// <summary>#1724 P2a: an action linked to a concept only by 'expresses' is in the concept's drill with its page, outside the product closure's cap.</summary>
public sealed class ConceptScopeExpressesTests
{
    private static readonly WholeMapBasis _basis = new("v7", MapCommit: null, Draft: false, DateTimeOffset.UnixEpoch, Repo: null);

    private static HashSet<string> Keys(ConceptGraph world)
    {
        var graph = world.Build();
        var ask = new WholeMapAsk(_basis, WholeMapDetail.Auto, [], Area: null) { Concept = "concept:scheduling" };
        return [.. WholeMapBuilder.Build(ask, graph, MapAreas.Assign(graph), WholeMapRules.Default, ConceptGraph.Text).Value.Nodes!.Select(n => n.Key.Value)];
    }

    [Fact, Trait("Lane", "unit")]
    public void A_concept_whose_only_link_to_an_action_is_expresses_lists_it()
    {
        var world = new ConceptGraph().Concept("concept:scheduling", "Self scheduling")
            .Node("feature:shifts").Member("feature:shifts", "concept:scheduling")
            .Node("surface:screen/week", "Schedule week").Node("action:submit", "Submit a schedule")
            .Edge("action:submit", EdgeType.On, "surface:screen/week").Edge("action:submit", EdgeType.Expresses, "concept:scheduling");

        var keys = Keys(world);

        Assert.Contains("action:submit", keys);
        Assert.Contains("surface:screen/week", keys);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_concept_with_a_200_node_closure_still_lists_its_pages()
    {
        // 250 actions write the thing the member decision decides: the closure fills its 200 with the smallest keys, and the linked action sorts after all of them.
        var world = new ConceptGraph().Concept("concept:scheduling", "Self scheduling")
            .Node("decision:rule").Member("decision:rule", "concept:scheduling").Node("thing:shift").Edge("decision:rule", EdgeType.Decides, "thing:shift");
        for (var i = 0; i < 250; i++)
        {
            var key = "action:a-" + i.ToString("D3", CultureInfo.InvariantCulture);
            _ = world.Node(key).Edge(key, EdgeType.Writes, "thing:shift");
        }

        _ = world.Node("surface:screen/zzz-week").Node("action:zzz-submit").Edge("action:zzz-submit", EdgeType.On, "surface:screen/zzz-week").Edge("action:zzz-submit", EdgeType.Expresses, "concept:scheduling");

        var keys = Keys(world);

        Assert.Contains("action:zzz-submit", keys);
        Assert.Contains("surface:screen/zzz-week", keys);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_page_leaves_pages_in_no_concept_when_one_of_its_actions_is_linked()
    {
        var graph = new ConceptGraph().Concept("concept:scheduling", "Self scheduling")
            .Node("surface:screen/week").Node("action:submit").Edge("action:submit", EdgeType.On, "surface:screen/week").Edge("action:submit", EdgeType.Expresses, "concept:scheduling")
            .Node("surface:screen/settings").Node("action:save").Edge("action:save", EdgeType.On, "surface:screen/settings")
            .Node("surface:screen/own").Edge("surface:screen/own", EdgeType.Expresses, "concept:scheduling")
            .Build();

        Assert.Equal(["surface:screen/settings"], PagesInNoConcept.Of(graph));
    }
}
