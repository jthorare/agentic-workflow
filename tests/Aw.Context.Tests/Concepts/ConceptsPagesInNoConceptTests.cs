using Aw.Context.Core.Concepts;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;

namespace Aw.Context.Tests.Concepts;

/// <summary>#1724 P2b-1b: the concepts answer counts the pages in no concept, so the Context head shows "Pages in no concept (N)" without reading the whole map at open (pages spec §3.4).</summary>
public sealed class ConceptsPagesInNoConceptTests
{
    private static ConceptsView Read(ConceptGraph graph) =>
        ConceptBuilder.Build(ConceptGraph.Basis, new ConceptsQuery(MapRead.Current, new ConceptPage()), graph.Build(), ConceptGraph.Text).Value;

    [Fact, Trait("Lane", "unit")]
    public void The_concepts_answer_counts_the_pages_in_no_concept()
    {
        var graph = new ConceptGraph().Concept("concept:scheduling", "Self scheduling")
            .Node("surface:screen/week", "Schedule week").Node("action:submit", "Submit schedule")
            .Edge("action:submit", EdgeType.On, "surface:screen/week").Edge("action:submit", EdgeType.Expresses, "concept:scheduling")
            .Node("surface:screen/settings", "Settings").Node("action:save", "Save").Edge("action:save", EdgeType.On, "surface:screen/settings")
            .OwnerNode("surface:screen/about", "About", "web/about");

        Assert.Equal(2, Read(graph).PagesInNoConcept);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_map_where_every_page_has_a_concept_counts_none() => Assert.Equal(0, Read(ConceptGraph.Product()).PagesInNoConcept);
}
