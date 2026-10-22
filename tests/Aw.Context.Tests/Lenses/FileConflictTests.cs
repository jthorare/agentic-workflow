using System.Globalization;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Lenses;

namespace Aw.Context.Tests.Lenses;

public sealed class FileConflictTests
{
    private static readonly DateTimeOffset _asOf = DateTimeOffset.Parse("2026-10-14T09:12:40Z", CultureInfo.InvariantCulture);

    private static NodeKey K(string key) => NodeKey.From(key);

    private static GraphEdge Edge(string from, string to, EdgeType type) => new(K(from), K(to), type);

    private static GraphSnapshot Graph(params GraphEdge[] edges)
    {
        var keys = edges.SelectMany(e => new[] { e.From, e.To }).DistinctBy(k => k.Value, StringComparer.Ordinal);
        return new GraphSnapshot([.. keys.Select(k => new GraphNode(k, k.Value))], edges, [], []);
    }

    private static IReadOnlyList<LensConflict> Conflicts(GraphSnapshot graph, string anchor = "feature:a")
        => LensesBuilder.Build(new LensBasis("v1@abc", Draft: false, _asOf), graph, K(anchor), depth: 1).Value.Conflicts;

    [Fact, Trait("Lane", "unit")]
    public void A_decision_citing_a_file_that_implements_only_something_else_is_a_contradicts_conflict_on_the_decided_node()
    {
        var graph = Graph(
            Edge("decision:d1", "feature:a", EdgeType.Decides),
            Edge("decision:d1", "code:src/x.ts", EdgeType.Decides),
            Edge("code:src/x.ts", "feature:b", EdgeType.Implements));

        var conflict = Assert.Single(Conflicts(graph));

        Assert.Equal((ConflictKind.Contradicts, "feature:a", "decision:d1", "code:src/x.ts"), (conflict.Kind, conflict.Node.Value, conflict.Decision.Value, conflict.Other));
        Assert.Contains("feature:b", conflict.Summary, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_file_that_also_implements_the_decided_node_is_no_conflict()
    {
        var graph = Graph(
            Edge("decision:d1", "feature:a", EdgeType.Decides),
            Edge("decision:d1", "code:src/x.ts", EdgeType.Decides),
            Edge("code:src/x.ts", "feature:b", EdgeType.Implements),
            Edge("code:src/x.ts", "feature:a", EdgeType.Implements));

        Assert.Empty(Conflicts(graph));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_file_that_implements_nothing_is_no_conflict()
    {
        var graph = Graph(Edge("decision:d1", "feature:a", EdgeType.Decides), Edge("decision:d1", "code:src/x.ts", EdgeType.Decides));

        Assert.Empty(Conflicts(graph));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("feature:parent")]
    [InlineData("feature:child")]
    public void A_file_that_implements_the_decided_nodes_part_of_parent_or_child_is_no_conflict(string implemented)
    {
        var graph = Graph(
            Edge("feature:a", "feature:parent", EdgeType.PartOf),
            Edge("feature:child", "feature:a", EdgeType.PartOf),
            Edge("decision:d1", "feature:a", EdgeType.Decides),
            Edge("decision:d1", "code:src/x.ts", EdgeType.Decides),
            Edge("code:src/x.ts", implemented, EdgeType.Implements));

        Assert.Empty(Conflicts(graph));
    }
}
