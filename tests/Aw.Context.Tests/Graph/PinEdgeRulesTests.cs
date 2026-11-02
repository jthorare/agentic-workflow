using Aw.Context.Core.Graph;

namespace Aw.Context.Tests.Graph;

/// <summary>Spec 2026-10-27 §10.2: a Concept joins another Concept by 'depends-on' and reads or writes a Thing; the table still refuses the mixed cases.</summary>
public sealed class PinEdgeRulesTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData(EdgeType.DependsOn, NodeKind.Concept, NodeKind.Concept, true)]
    [InlineData(EdgeType.DependsOn, NodeKind.Concept, NodeKind.Feature, false)]
    [InlineData(EdgeType.DependsOn, NodeKind.Feature, NodeKind.Concept, false)]
    [InlineData(EdgeType.Reads, NodeKind.Concept, NodeKind.Thing, true)]
    [InlineData(EdgeType.Writes, NodeKind.Concept, NodeKind.Thing, true)]
    [InlineData(EdgeType.Reads, NodeKind.Concept, NodeKind.Surface, false)]
    [InlineData(EdgeType.Writes, NodeKind.Concept, NodeKind.Concept, false)]
    public void The_edge_table_allows_the_pin_edges_and_refuses_the_mixed_ones(EdgeType type, NodeKind from, NodeKind to, bool allowed) =>
        Assert.Equal(allowed, EdgeRules.Allows(type, from, to));
}
