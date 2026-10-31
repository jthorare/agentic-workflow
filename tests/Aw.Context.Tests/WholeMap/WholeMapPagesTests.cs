using Aw.Context.Core.Graph;
using Aw.Context.Core.WholeMap;
using Aw.Context.Tests.Concepts;

namespace Aw.Context.Tests.WholeMap;

/// <summary>#1724 P2b: a concept open and the pages in no concept carry their pages in the section's order and mark the owner-named ones, so the app lists them as the spec says (§3.4, §3.5).</summary>
public sealed class WholeMapPagesTests
{
    private static readonly WholeMapBasis _basis = new("v7", MapCommit: null, Draft: false, DateTimeOffset.UnixEpoch, Repo: null);

    private static WholeMapView Read(ConceptGraph world, string? concept = null, bool pagesInNoConcept = false)
    {
        var graph = world.Build();
        var ask = new WholeMapAsk(_basis, WholeMapDetail.Auto, [], Area: null) { Concept = concept, PagesInNoConcept = pagesInNoConcept };
        return WholeMapBuilder.Build(ask, graph, MapAreas.Assign(graph), WholeMapRules.Default, ConceptGraph.Text).Value;
    }

    // Self scheduling: an owner-named page with one action, and two suggested pages with one and two actions, each action expressing the concept.
    private static ConceptGraph Scheduling() => new ConceptGraph().Concept("concept:scheduling", "Self scheduling")
        .Node("feature:shifts").Member("feature:shifts", "concept:scheduling")
        .Node("surface:screen/availability", "Availability").Node("action:save", "Save availability")
        .Edge("action:save", EdgeType.On, "surface:screen/availability").Edge("action:save", EdgeType.Expresses, "concept:scheduling")
        .Node("surface:screen/week", "Schedule week").Node("action:submit", "Submit schedule").Node("action:copy", "Copy last week")
        .Edge("action:submit", EdgeType.On, "surface:screen/week").Edge("action:submit", EdgeType.Expresses, "concept:scheduling")
        .Edge("action:copy", EdgeType.On, "surface:screen/week").Edge("action:copy", EdgeType.Expresses, "concept:scheduling")
        .OwnerNode("surface:screen/time-off", "Time-off request", "web/time-off").OwnerNode("action:send", "Send a request", "web/time-off")
        .Edge("action:send", EdgeType.On, "surface:screen/time-off").Edge("action:send", EdgeType.Expresses, "concept:scheduling");

    [Fact, Trait("Lane", "unit")]
    public void A_concept_open_lists_its_owner_page_first_then_suggested_by_number_of_actions()
    {
        var view = Read(Scheduling(), concept: "concept:scheduling");

        Assert.Equal(["surface:screen/time-off", "surface:screen/week", "surface:screen/availability"], view.Pages);
    }

    [Fact, Trait("Lane", "unit")]
    public void Only_the_owner_named_nodes_are_marked_owner()
    {
        var view = Read(Scheduling(), concept: "concept:scheduling");

        Assert.Equal(["action:send", "surface:screen/time-off"], [.. view.Nodes!.Where(static n => n.Owner).Select(static n => n.Key.Value).Order(StringComparer.Ordinal)]);
    }

    [Fact, Trait("Lane", "unit")]
    public void Pages_in_no_concept_carry_their_pages_in_the_section_order()
    {
        var world = new ConceptGraph()
            .Node("surface:screen/settings", "Settings").Node("action:save", "Save").Edge("action:save", EdgeType.On, "surface:screen/settings")
            .OwnerNode("surface:screen/about", "About", "web/about");

        var view = Read(world, pagesInNoConcept: true);

        Assert.Equal(["surface:screen/about", "surface:screen/settings"], view.Pages);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_answer_that_opens_no_concept_has_no_pages() => Assert.Null(Read(Scheduling()).Pages);
}
