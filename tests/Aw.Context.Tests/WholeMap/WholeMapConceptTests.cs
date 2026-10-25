using Aw.Context.Core;
using Aw.Context.Core.Graph;
using Aw.Context.Core.WholeMap;
using Aw.Context.Tests.Concepts;

namespace Aw.Context.Tests.WholeMap;

/// <summary>The whole map with the concept layer: concept nodes are their own band and never an area, 'concept=' scopes the answer like 'area=', and a located key names its concepts.</summary>
public sealed class WholeMapConceptTests
{
    private static readonly WholeMapBasis _basis = new("v7", MapCommit: null, Draft: false, DateTimeOffset.UnixEpoch, Repo: null);

    private static Kernel.Result<WholeMapView> Ask(ConceptGraph world, string? concept = null, string? area = null, string[]? locate = null)
    {
        var graph = world.Build();
        var ask = new WholeMapAsk(_basis, WholeMapDetail.Auto, locate ?? [], area) { Concept = concept };
        return WholeMapBuilder.Build(ask, graph, MapAreas.Assign(graph), WholeMapRules.Default, ConceptGraph.Text);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_concept_node_is_in_no_area_and_the_counts_have_a_concept_row()
    {
        var view = Ask(ConceptGraph.Product()).Value;

        Assert.DoesNotContain(view.Areas, a => a.Key.StartsWith("concept:", StringComparison.Ordinal) || a.ByKind.ContainsKey("concept"));
        Assert.Equal(2, view.Counts.ByKind["concept"]);
        Assert.Equal(2, view.Nodes!.Count(n => n.Kind == NodeKind.Concept));
        Assert.All(view.Nodes!.Where(n => n.Kind == NodeKind.Concept), n => Assert.Equal(AreaIndex.ConceptBandKey, n.Area));
        Assert.DoesNotContain(view.AreaEdges, e => e.Type == EdgeType.BelongsTo);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_concept_scopes_the_nodes_to_its_members_and_two_levels_below_and_leaves_other_concepts_out()
    {
        var view = Ask(ConceptGraph.Product(), concept: "concept:scheduling").Value;

        Assert.Equal(WholeMapDetail.Nodes, view.Detail);
        Assert.Equal(
            ["action:pick", "code:src/Shared.cs", "code:src/Shifts.cs", "concept:scheduling", "feature:shifts", "feature:swaps", "record:swap-rules", "surface:screen/shifts"],
            view.Nodes!.Select(n => n.Key.Value).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.All(view.Edges!, e => Assert.Contains(view.Nodes!, n => n.Key.Value == e.From.Value));
        Assert.Equal(3, view.Edges!.Count(e => e.Type == EdgeType.BelongsTo));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_other_concept_scopes_to_the_members_no_concept_takes_even_when_the_map_has_no_node_for_it()
    {
        var view = Ask(ConceptGraph.Product(), concept: ConceptKeys.Other).Value;

        Assert.Equal(["feature:lone", "record:orphan"], view.Nodes!.Select(n => n.Key.Value).Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_concept_the_map_lacks_is_200_and_a_concept_with_an_area_is_006()
    {
        var unknown = Ask(ConceptGraph.Product(), concept: "concept:nope");
        var both = Ask(ConceptGraph.Product(), concept: "concept:billing", area: "feature:shifts");

        Assert.Equal((ContextErrors.AreaUnknown.Code, ContextErrors.QueryInvalid.Code), (unknown.Error.Code, both.Error.Code));
    }

    [Fact, Trait("Lane", "unit")]
    public void Located_lists_the_concepts_holding_each_key_other_for_an_unassigned_member_and_none_for_the_rest()
    {
        var located = Ask(ConceptGraph.Product(), locate: ["feature:shifts", "code:src/Shifts.cs", "feature:lone", "feature:nope", "concept:billing"]).Value.Located!;

        Assert.Equal(["concept:scheduling"], located[0].Concept);
        Assert.Empty(located[1].Concept);
        Assert.Equal([ConceptKeys.Other], located[2].Concept);
        Assert.Empty(located[3].Concept);
        Assert.Empty(located[4].Concept);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_member_of_two_concepts_is_located_in_both_by_key()
    {
        var world = ConceptGraph.Product().Member("feature:shifts", "concept:billing");

        var located = Ask(world, locate: ["feature:shifts"]).Value.Located!;

        Assert.Equal(["concept:billing", "concept:scheduling"], located.Single().Concept);
    }
}
