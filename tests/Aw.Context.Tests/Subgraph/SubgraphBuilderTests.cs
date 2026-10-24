using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Subgraph;
using Aw.Context.Testing.Graph;

namespace Aw.Context.Tests.Subgraph;

public sealed class SubgraphBuilderTests
{
    private const string A = "feature:a";
    private const string B = "feature:b";
    private const string Surface = "surface:command/x";
    private const string Act = "action:do";
    private const string Rec = "record:linear:AB-1";
    private const string D1 = "decision:d1";
    private const string D0 = "decision:d0";
    private const string FileA = "code:src/a.ts";
    private const string FileB = "code:src/b.ts";
    private static readonly DateTimeOffset _asOf = DateTimeOffset.Parse("2026-10-14T09:12:40Z", CultureInfo.InvariantCulture);

    private static readonly LensBasis _basis = new("v1", Draft: false, _asOf);

    // A is part of nothing; B and the surface are part of A; the action is on the surface; a record mentions A; D1 decides A and supersedes D0 (which decides A too);
    // two files implement A and B. C is a lone feature decided by E1, which supersedes E0.
    private static GraphSnapshot Graph() => new GraphBuilder()
        .Edge(B, EdgeType.PartOf, A).Edge(Surface, EdgeType.PartOf, A).Edge(Act, EdgeType.On, Surface)
        .Edge(Rec, EdgeType.Mentions, A).Edge(D1, EdgeType.Decides, A).Edge(D0, EdgeType.Decides, A).Edge(D1, EdgeType.Supersedes, D0)
        .Edge(FileA, EdgeType.Implements, A).Edge(FileB, EdgeType.Implements, B)
        .Edge("decision:e1", EdgeType.Decides, "feature:c").Edge("decision:e0", EdgeType.Decides, "feature:c").Edge("decision:e1", EdgeType.Supersedes, "decision:e0")
        .Build();

    private static SubgraphView Of(string[] keys, int depth = 0, GraphSnapshot? graph = null) =>
        SubgraphBuilder.Build(_basis, graph ?? Graph(), keys, depth, []).Value;

    [Fact, Trait("Lane", "unit")]
    public void Given_keys_are_placed_by_kind_in_the_three_bands()
    {
        var view = Of([A, Rec, D1, FileA]);

        Assert.Equal([A, Rec, D1, FileA], view.Nodes.Select(n => n.Value), StringComparer.Ordinal);
        Assert.Equal([A], view.Bands.Product.Own.Select(i => i.Node.Value), StringComparer.Ordinal);
        Assert.Equal([Rec], view.Bands.Intent.Records.Select(r => r.Key.Value), StringComparer.Ordinal);
        Assert.Equal([D1], view.Bands.Intent.Decisions.Select(d => d.Node.Value), StringComparer.Ordinal);
        Assert.Equal(["src/a.ts"], view.Bands.Code.Files.Select(f => f.Path), StringComparer.Ordinal);
        Assert.Equal(1, view.SchemaVersion);
        Assert.Equal([0, 0, 0, 0], new[] { view.Bands.Product.Own[0].Depth, view.Bands.Intent.Records[0].Depth, view.Bands.Intent.Decisions[0].Depth, view.Bands.Code.Files[0].Depth }.Select(d => d ?? -1));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_set_of_only_code_nodes_still_gets_a_map_coverage_over_their_directories()
    {
        var view = Of([FileA]);

        Assert.Equal(["src/"], view.Coverage.Map!.Paths);
    }

    [Fact, Trait("Lane", "unit")]
    public void Widened_code_files_add_their_directories_to_the_map_coverage()
    {
        var graph = new GraphBuilder().Edge(B, EdgeType.PartOf, A).Edge("code:lib/b.ts", EdgeType.Implements, B).Edge(FileA, EdgeType.Implements, A).Build();

        var view = SubgraphBuilder.Build(_basis, graph, [A], 2, []).Value;

        Assert.Equal(["lib/", "src/"], view.Coverage.Map!.Paths);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_product_band_lists_every_neighbor_and_edge_beyond_100()
    {
        var builder = new GraphBuilder();
        foreach (var i in Enumerable.Range(0, 105))
        {
            _ = builder.Edge(string.Create(CultureInfo.InvariantCulture, $"feature:n{i}"), EdgeType.PartOf, A);
        }

        var product = SubgraphBuilder.Build(_basis, builder.Build(), [A], 1, []).Value.Bands.Product;

        Assert.Equal((105, 105), (product.Neighbors.Count, product.Edges.Count));
        Assert.Equal((105, 105), (product.Counts.Neighbors, product.Counts.Edges));
    }

    [Fact, Trait("Lane", "unit")]
    public void Edges_are_only_among_returned_nodes()
    {
        var view = Of([A, FileA]);

        var returned = view.Nodes.Select(n => n.Value).ToHashSet(StringComparer.Ordinal);
        Assert.All(view.Edges, e => Assert.True(returned.Contains(e.From.Value) && returned.Contains(e.To.Value)));
        var edge = Assert.Single(view.Edges);
        Assert.Equal((FileA, A, EdgeType.Implements), (edge.From.Value, edge.To.Value, edge.Type));
    }

    [Fact, Trait("Lane", "unit")]
    public void Depth_0_returns_exactly_the_given_keys()
    {
        var view = Of([A, B]);

        Assert.Empty(view.Bands.Product.Neighbors);
        Assert.Empty(view.Bands.Product.Affects);
        Assert.Empty(view.Bands.Intent.Records);
        Assert.Empty(view.Bands.Code.Files);
        Assert.Equal([A, B], view.Bands.Product.Own.Select(i => i.Node.Value), StringComparer.Ordinal);
        Assert.All(view.Bands.Product.Own, i => Assert.Equal(0, i.Depth));
    }

    [Fact, Trait("Lane", "unit")]
    public void Depth_2_widens_through_neighbors_and_marks_depth()
    {
        var view = Of([A], depth: 2);

        var near = view.Bands.Product.Neighbors.ToDictionary(n => n.Node.Value, n => n.Depth, StringComparer.Ordinal);
        Assert.Equal((1, 1, 2), (near[B], near[Surface], near[Act]));
        Assert.Equal([1], view.Bands.Intent.Records.Select(r => r.Depth ?? -1));
        Assert.Equal([1, 1], view.Bands.Intent.Decisions.Select(d => d.Depth ?? -1));
        Assert.Equal([1, 2], view.Bands.Code.Files.Select(f => f.Depth ?? -1).Order());   // a.ts implements A (1 hop); b.ts implements B, one more
        Assert.Equal([A], view.Nodes.Select(n => n.Value), StringComparer.Ordinal);
        Assert.Contains(view.Bands.Product.Neighbors, n => n.Node.Value == Act && n.Path.Count == 2);
    }

    [Fact, Trait("Lane", "unit")]
    public void Absent_key_is_in_missing_in_request_order()
    {
        var view = Of(["feature:zzz", A, "feature:yyy", "feature:zzz"]);

        Assert.Equal(["feature:zzz", "feature:yyy"], view.Missing, StringComparer.Ordinal);
        Assert.Equal([A], view.Nodes.Select(n => n.Value), StringComparer.Ordinal);
        Assert.Equal([A], view.Searched.Anchors, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Malformed_key_is_006_naming_it()
    {
        var result = SubgraphBuilder.Build(_basis, Graph(), [A, "not a key"], 0, []);

        Assert.True(result.IsError);
        Assert.Equal(ContextErrors.QueryInvalid.Code, result.Error.Code);
        Assert.Contains("not a key", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Band_over_100_lists_every_node()
    {
        var builder = new GraphBuilder();
        var keys = Enumerable.Range(0, 150).Select(i => string.Create(CultureInfo.InvariantCulture, $"feature:f{i:D3}")).ToArray();
        foreach (var key in keys)
        {
            _ = builder.Node(key);
        }

        var view = Of(keys, graph: builder.Build());

        Assert.Equal(150, view.Bands.Product.Counts.Own);
        Assert.Equal(keys, view.Bands.Product.Own.Select(i => i.Node.Value), StringComparer.Ordinal);
        Assert.Equal(150, view.Nodes.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public void Coverage_is_over_the_given_product_nodes()
    {
        var view = Of([A, B, Rec, FileA], depth: 1);

        Assert.Equal(2, view.Coverage.Scope);
        Assert.Equal((1, 2), (view.Coverage.Intent.Covered, view.Coverage.Intent.Of));
        Assert.Equal(2, view.Coverage.Code.Of);
        Assert.NotNull(view.Coverage.Map);
        Assert.Null(view.Coverage.ByAnchor);
    }

    [Fact, Trait("Lane", "unit")]
    public void Conflicts_on_returned_nodes_only()
    {
        var withA = Of([A]);
        var withC = Of(["feature:c"]);

        Assert.All(withA.Conflicts, c => Assert.Equal(A, c.Node.Value));
        _ = Assert.Single(withA.Conflicts);
        Assert.All(withC.Conflicts, c => Assert.Equal("feature:c", c.Node.Value));
        Assert.Empty(Of([B]).Conflicts);
    }

    [Fact, Trait("Lane", "unit")]
    public void Any_number_of_keys_is_taken_and_none_is_006()
    {
        var keys = Enumerable.Range(0, 1_500).Select(i => string.Create(CultureInfo.InvariantCulture, $"feature:f{i}")).ToArray();   // the 1000-key limit was removed per owner q93, 2026-10-23

        var many = SubgraphBuilder.Build(_basis, Graph(), keys, 0, []);
        var none = SubgraphBuilder.Build(_basis, Graph(), [], 0, []);

        Assert.True(many.IsSuccess);
        Assert.Equal(1_500, many.Value.Missing.Count);
        Assert.Equal(ContextErrors.QueryInvalid.Code, none.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_negative_depth_is_006()
    {
        var result = SubgraphBuilder.Build(_basis, Graph(), [A], -1, []);

        Assert.True(result.IsError);
        Assert.Equal(ContextErrors.QueryInvalid.Code, result.Error.Code);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(5)]
    [InlineData(99)]
    public void A_depth_past_the_old_cap_widens_and_one_past_8_is_read_as_8(int depth) =>
        Assert.True(SubgraphBuilder.Build(_basis, Graph(), [A], depth, []).IsSuccess);
}
