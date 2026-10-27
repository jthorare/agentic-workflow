using System.Globalization;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Lenses;

namespace Aw.Context.Tests.Lenses;

/// <summary>A lens's product neighbors are the features structurally connected to the anchor, not every feature that only shares a hub (concept, record, code unit) with it (#1339).</summary>
public sealed class LensesHubSiblingTests
{
    private static readonly DateTimeOffset _asOf = DateTimeOffset.Parse("2026-10-14T09:12:40Z", CultureInfo.InvariantCulture);

    [Theory, Trait("Lane", "unit")]
    [InlineData(1)]
    [InlineData(2)]
    public void A_feature_that_only_shares_a_concept_with_the_anchor_is_not_a_neighbor(int depth)
    {
        var graph = Graph(["feature:a", "feature:b", "concept:c"], Edge("feature:a", "concept:c", EdgeType.BelongsTo), Edge("feature:b", "concept:c", EdgeType.BelongsTo));

        Assert.DoesNotContain("feature:b", Neighbors(graph, depth), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_depends_on_edge_still_makes_a_neighbor_while_the_concept_sibling_does_not()
    {
        var graph = Graph(
            ["feature:a", "feature:b", "feature:d", "concept:c"],
            Edge("feature:a", "concept:c", EdgeType.BelongsTo),
            Edge("feature:b", "concept:c", EdgeType.BelongsTo),
            Edge("feature:a", "feature:d", EdgeType.DependsOn));

        var neighbors = Neighbors(graph, depth: 1);

        Assert.Contains("feature:d", neighbors, StringComparer.Ordinal);
        Assert.DoesNotContain("feature:b", neighbors, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_feature_that_only_shares_a_mentioning_record_with_the_anchor_is_not_a_neighbor()
    {
        var graph = Graph(["feature:a", "feature:b", "record:linear:issue/1"], Edge("record:linear:issue/1", "feature:a", EdgeType.Mentions), Edge("record:linear:issue/1", "feature:b", EdgeType.Mentions));

        Assert.DoesNotContain("feature:b", Neighbors(graph, depth: 2), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_feature_that_only_shares_a_code_unit_with_the_anchor_is_not_a_neighbor_without_the_component_hop()
    {
        var graph = Graph(["feature:a", "feature:b", "code:src/shared.ts"], Edge("code:src/shared.ts", "feature:a", EdgeType.Implements), Edge("code:src/shared.ts", "feature:b", EdgeType.Implements));

        Assert.DoesNotContain("feature:b", Neighbors(graph, depth: 2), StringComparer.Ordinal);
    }

    private static string[] Neighbors(GraphSnapshot graph, int depth) =>
        [.. LensesBuilder.Build(new LensBasis("v1", Draft: false, _asOf), graph, NodeKey.From("feature:a"), depth).Value.Bands.Product.Neighbors.Select(n => n.Node.Value)];

    private static GraphEdge Edge(string from, string to, EdgeType type) => new(NodeKey.From(from), NodeKey.From(to), type);

    private static GraphSnapshot Graph(string[] keys, params GraphEdge[] edges) =>
        new([.. keys.Select(k => new GraphNode(NodeKey.From(k), k))], [.. edges], Aliases: [], Decisions: []);
}
