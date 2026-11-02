using System.Globalization;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Lenses;
using Aw.Context.Tests.Extract;

namespace Aw.Context.Tests.Lenses;

/// <summary>Spec 2026-10-27 §3 "Worked example": a lens on 'orders' reaches the rolled-up edges.</summary>
public sealed class RollupLensTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData(1)]
    [InlineData(3)]
    public void A_lens_on_orders_returns_two_features_two_things_and_the_eight_edges_among_them(int depth)
    {
        var map = FeatureRollup.Apply(RollupFixture.Files());
        var graph = new GraphSnapshot(map.Nodes, map.Edges, [], []);

        var product = LensesBuilder.Build(new LensBasis("v1@abc", Draft: false, DateTimeOffset.Parse("2026-10-27T12:00:00Z", CultureInfo.InvariantCulture)), graph, NodeKey.From(RollupFixture.Orders), depth).Value.Bands.Product;

        var seen = product.Neighbors.Select(n => n.Node).Concat(product.Affects.Select(n => n.Node)).ToList();
        Assert.Equal(2, seen.Count(k => k.Kind == NodeKind.Feature));
        Assert.Equal(2, seen.Count(k => k.Kind == NodeKind.Thing));
        Assert.Equal(8, product.Edges.Count);
        Assert.Equal(2, product.Edges.Count(e => e.Type == EdgeType.DependsOn));
    }
}
