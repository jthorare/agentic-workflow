using Aw.Context.Core.Graph;
using Aw.Context.Testing.Graph;

namespace Aw.Context.Tests.Graph;

/// <summary>The concept kind: its key scheme, key casing, the belongs-to edge table and the cascade from a concept anchor.</summary>
public sealed class ConceptKindTests
{
    [Fact, Trait("Lane", "unit")]
    public void A_concept_key_names_the_concept_kind()
    {
        var key = NodeKey.From("concept:scheduling");

        Assert.Equal(NodeKind.Concept, key.Kind);
        Assert.Equal("scheduling", key.Name);
        Assert.Equal(NodeKind.Concept, NodeKinds.FromScheme("concept"));
    }

    [Fact, Trait("Lane", "unit")]
    public void EntryKey_lower_cases_a_concept_name()
    {
        Assert.Equal("concept:self-scheduling", EntryKey.For(NodeKey.From("concept:Self-Scheduling  ")).Value);
        Assert.Equal(1, EntryKey.Version);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_reserved_other_concept_is_a_valid_concept_key()
    {
        Assert.Equal("concept:other", ConceptKeys.Other);
        Assert.Equal(NodeKind.Concept, NodeKey.From(ConceptKeys.Other).Kind);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(NodeKind.Feature, NodeKind.Concept, true)]
    [InlineData(NodeKind.Record, NodeKind.Concept, true)]
    [InlineData(NodeKind.Decision, NodeKind.Concept, true)]
    [InlineData(NodeKind.Surface, NodeKind.Concept, false)]
    [InlineData(NodeKind.Action, NodeKind.Concept, false)]
    [InlineData(NodeKind.Thing, NodeKind.Concept, false)]
    [InlineData(NodeKind.Flow, NodeKind.Concept, false)]
    [InlineData(NodeKind.Code, NodeKind.Concept, false)]
    [InlineData(NodeKind.Concept, NodeKind.Concept, false)]
    [InlineData(NodeKind.Concept, NodeKind.Feature, false)]
    [InlineData(NodeKind.Feature, NodeKind.Feature, false)]
    public void Belongs_to_joins_a_member_to_a_concept_and_nothing_else(NodeKind from, NodeKind to, bool allowed) =>
        Assert.Equal(allowed, EdgeRules.Allows(EdgeType.BelongsTo, from, to));

    [Fact, Trait("Lane", "unit")]
    public void Belongs_to_is_named_as_map_files_write_it() => Assert.Equal("belongs-to", EdgeRules.Name(EdgeType.BelongsTo));

    [Fact, Trait("Lane", "unit")]
    public void A_concept_anchor_expands_to_its_member_features_and_their_scope()
    {
        var graph = new GraphBuilder()
            .Edge("feature:shifts", EdgeType.BelongsTo, "concept:scheduling")
            .Edge("feature:swaps", EdgeType.BelongsTo, "concept:scheduling")
            .Edge("surface:page/shift-board", EdgeType.PartOf, "feature:shifts")
            .Edge("feature:invoices", EdgeType.BelongsTo, "concept:billing")
            .Build();

        var result = Cascade.Affects(graph, NodeKey.From("concept:scheduling"), depth: 1);

        Assert.Equal(
            ["concept:scheduling", "feature:shifts", "feature:swaps", "surface:page/shift-board"],
            result.OwnScope.Select(k => k.Value).Order(StringComparer.Ordinal),
            StringComparer.Ordinal);
        Assert.DoesNotContain(result.OwnScope, k => k.Value == "feature:invoices");
    }
}
