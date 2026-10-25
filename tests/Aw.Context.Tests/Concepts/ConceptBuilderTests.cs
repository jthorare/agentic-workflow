using Aw.Context.Core;
using Aw.Context.Core.Concepts;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;

namespace Aw.Context.Tests.Concepts;

public sealed class ConceptBuilderTests
{
    private static ConceptsView Read(ConceptGraph graph, string? q = null, ConceptPage? page = null) =>
        ConceptBuilder.Build(ConceptGraph.Basis, new ConceptsQuery(MapRead.Current, page ?? new ConceptPage(), Q: q), graph.Build(), ConceptGraph.Text).Value;

    [Fact, Trait("Lane", "unit")]
    public void Each_concept_names_itself_its_summary_source_size_and_citations_by_name()
    {
        var view = Read(ConceptGraph.Product());

        Assert.Equal((1, "v7", ConceptStates.Ok, null), (view.ApiVersion, view.MapVersion, view.State, view.Code));
        Assert.Equal(["concept:billing", "concept:scheduling"], view.Concepts.Select(c => c.Id.Value), StringComparer.Ordinal);
        var scheduling = view.Concepts.Single(c => c.Id.Value == "concept:scheduling");
        Assert.Equal(("Self scheduling", "People pick and swap shifts.", "derived"), (scheduling.Name, scheduling.Summary, scheduling.Source));
        Assert.Equal(["repo:web/docs/scheduling.md"], scheduling.Citations.Select(c => c.Key), StringComparer.Ordinal);
        Assert.Equal("override", view.Concepts.Single(c => c.Id.Value == "concept:billing").Source);
    }

    [Fact, Trait("Lane", "unit")]
    public void Size_counts_member_features_records_decisions_and_the_distinct_code_their_features_implement()
    {
        var view = Read(ConceptGraph.Product());

        Assert.Equal(new ConceptSize(2, 1, 0, 2), view.Concepts.Single(c => c.Id.Value == "concept:scheduling").Size);   // Shared.cs implements both features and counts once
        Assert.Equal(new ConceptSize(1, 0, 1, 1), view.Concepts.Single(c => c.Id.Value == "concept:billing").Size);
    }

    [Fact, Trait("Lane", "unit")]
    public void Other_holds_the_features_records_and_decisions_no_concept_takes()
    {
        var view = Read(ConceptGraph.Product());

        Assert.Equal((ConceptKeys.Other, "Other", new ConceptSize(1, 1, 0, 0)), (view.Other.Id.Value, view.Other.Name, view.Other.Size));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_member_of_two_concepts_counts_in_both_and_not_in_other()
    {
        var graph = ConceptGraph.Product().Member("feature:lone", "concept:billing").Member("feature:lone", "concept:scheduling");

        var view = Read(graph);

        Assert.Equal((3, 2, 0), (view.Concepts.Single(c => c.Id.Value == "concept:scheduling").Size.Features, view.Concepts.Single(c => c.Id.Value == "concept:billing").Size.Features, view.Other.Size.Features));
    }

    [Fact, Trait("Lane", "unit")]
    public void Links_weigh_the_edges_between_members_of_two_concepts_and_skip_edges_inside_one()
    {
        var view = Read(ConceptGraph.Product());

        Assert.Equal(
            [("concept:billing", "concept:scheduling", 1), ("concept:other", "concept:scheduling", 1), ("concept:scheduling", "concept:billing", 1)],
            view.Links.Select(l => (l.From.Value, l.To.Value, l.Weight)));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_map_with_no_concept_is_empty_and_every_member_is_in_other()
    {
        var graph = new ConceptGraph().Node("feature:a").Node("feature:b").Node("record:r").Node("surface:s");

        var view = Read(graph);

        Assert.Equal((ConceptStates.Empty, 0, new ConceptSize(2, 1, 0, 0)), (view.State, view.Concepts.Count, view.Other.Size));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("scheduling", "concept:scheduling")]
    [InlineData("PAYMENT", "concept:billing")]
    [InlineData("round up", "concept:billing")]
    [InlineData("swap rules", "concept:scheduling")]
    public void The_filter_matches_a_name_a_summary_or_a_member_label_ignoring_case(string q, string expected) =>
        Assert.Equal([expected], Read(ConceptGraph.Product(), q).Concepts.Select(c => c.Id.Value), StringComparer.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void A_filter_that_matches_nothing_answers_no_concepts_and_still_the_state_and_other()
    {
        var view = Read(ConceptGraph.Product(), "zzz");

        Assert.Equal((ConceptStates.Ok, 0, ConceptKeys.Other), (view.State, view.Concepts.Count, view.Other.Id.Value));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_page_answers_its_limit_and_the_cursor_of_the_next_until_the_last()
    {
        var first = Read(ConceptGraph.Product(), page: new ConceptPage(Limit: 1));
        var second = Read(ConceptGraph.Product(), page: new ConceptPage(first.Next, 1));

        Assert.Equal(["concept:billing"], first.Concepts.Select(c => c.Id.Value), StringComparer.Ordinal);
        Assert.Equal("v7:1", first.Next);
        Assert.Equal(["concept:scheduling"], second.Concepts.Select(c => c.Id.Value), StringComparer.Ordinal);
        Assert.Null(second.Next);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_page_carries_the_links_with_an_end_on_it()
    {
        var first = Read(ConceptGraph.Product(), page: new ConceptPage(Limit: 1));

        Assert.All(first.Links, l => Assert.True(l.From.Value == "concept:billing" || l.To.Value == "concept:billing"));
        Assert.NotEmpty(first.Links);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("v6:1", null)]
    [InlineData("nonsense", null)]
    [InlineData(null, 0)]
    [InlineData(null, -3)]
    public void A_cursor_from_another_map_version_or_a_limit_below_one_is_006(string? cursor, int? limit)
    {
        var result = ConceptBuilder.Build(ConceptGraph.Basis, new ConceptsQuery(MapRead.Current, new ConceptPage(cursor, limit)), ConceptGraph.Product().Build(), ConceptGraph.Text);

        Assert.Equal(ContextErrors.QueryInvalid.Code, result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void There_is_no_limit_on_the_page_size()
    {
        var view = Read(ConceptGraph.Product(), page: new ConceptPage(Limit: int.MaxValue));

        Assert.Equal((2, null), (view.Concepts.Count, view.Next));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_members_of_a_concept_are_its_features_records_and_decisions_by_kind_and_key()
    {
        var view = ConceptBuilder.Members(ConceptGraph.Basis, new ConceptMembersQuery(NodeKey.From("concept:scheduling"), MapRead.Current, new ConceptPage()), ConceptGraph.Product().Build(), ConceptGraph.Text).Value;

        Assert.Equal(("concept:scheduling", "v7"), (view.Concept.Value, view.MapVersion));
        Assert.Equal([("feature:shifts", "feature", "Shifts"), ("feature:swaps", "feature", "Swaps"), ("record:swap-rules", "record", "Swap rules")], view.Members.Select(m => (m.Key, m.Kind, m.Label)));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_members_of_other_are_the_ones_no_concept_takes()
    {
        var view = ConceptBuilder.Members(ConceptGraph.Basis, new ConceptMembersQuery(NodeKey.From(ConceptKeys.Other), MapRead.Current, new ConceptPage()), ConceptGraph.Product().Build(), ConceptGraph.Text).Value;

        Assert.Equal(["feature:lone", "record:orphan"], view.Members.Select(m => m.Key), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_members_page_by_the_same_cursor_and_an_unknown_concept_is_003()
    {
        var first = ConceptBuilder.Members(ConceptGraph.Basis, new ConceptMembersQuery(NodeKey.From("concept:scheduling"), MapRead.Current, new ConceptPage(Limit: 2)), ConceptGraph.Product().Build(), ConceptGraph.Text).Value;
        var rest = ConceptBuilder.Members(ConceptGraph.Basis, new ConceptMembersQuery(NodeKey.From("concept:scheduling"), MapRead.Current, new ConceptPage(first.Next, 2)), ConceptGraph.Product().Build(), ConceptGraph.Text).Value;
        var unknown = ConceptBuilder.Members(ConceptGraph.Basis, new ConceptMembersQuery(NodeKey.From("concept:nope"), MapRead.Current, new ConceptPage()), ConceptGraph.Product().Build(), ConceptGraph.Text);

        Assert.Equal((2, "v7:2"), (first.Members.Count, first.Next));
        Assert.Equal(["record:swap-rules"], rest.Members.Select(m => m.Key), StringComparer.Ordinal);
        Assert.Null(rest.Next);
        Assert.Equal(ContextErrors.UnknownAnchor.Code, unknown.Error.Code);
    }
}
