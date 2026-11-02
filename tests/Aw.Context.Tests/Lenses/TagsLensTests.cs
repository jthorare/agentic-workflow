using System.Globalization;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Results;
using Aw.Context.Testing.Graph;

namespace Aw.Context.Tests.Lenses;

/// <summary>Spec 2026-10-27 §2.3 "What the lens walks": 'tags' and 'implements' are not structure edges, so two modules a model tagged alike are not neighbors.</summary>
public sealed class TagsLensTests
{
    private static LensesView Lens(GraphSnapshot graph, string anchor) =>
        LensesBuilder.Build(new LensBasis("v1@abc", Draft: false, DateTimeOffset.Parse("2026-10-27T12:00:00Z", CultureInfo.InvariantCulture)), graph, NodeKey.From(anchor), depth: 3).Value;

    private static GraphBuilder Modules() => new GraphBuilder()
        .Node("feature:work.a", provenance: Provenance.Deterministic).Node("feature:work.b", provenance: Provenance.Deterministic).Node("feature:work.c", provenance: Provenance.Deterministic)
        .Node("feature:tag:work:auth", provenance: Provenance.Model, confidence: 0.7)
        .Edge("code:src/a.ts", EdgeType.Implements, "feature:work.a", provenance: Provenance.Deterministic)
        .Edge("code:src/b.ts", EdgeType.Implements, "feature:work.b", provenance: Provenance.Deterministic)
        .Edge("code:src/a.ts", EdgeType.Tags, "feature:tag:work:auth", provenance: Provenance.Model, confidence: 0.7)
        .Edge("code:src/b.ts", EdgeType.Tags, "feature:tag:work:auth", provenance: Provenance.Model, confidence: 0.7)
        .Edge("feature:work.a", EdgeType.DependsOn, "feature:work.c", provenance: Provenance.Deterministic);

    [Fact, Trait("Lane", "unit")]
    public void Modules_a_model_tagged_alike_are_not_neighbors_but_a_depends_on_edge_makes_one()
    {
        var product = Lens(Modules().Build(), "feature:work.a").Bands.Product;

        var seen = product.Neighbors.Select(n => n.Node.Value).Concat(product.Affects.Select(n => n.Node.Value)).ToList();
        Assert.Contains("feature:work.c", seen, StringComparer.Ordinal);
        Assert.DoesNotContain("feature:work.b", seen, StringComparer.Ordinal);
        Assert.DoesNotContain("feature:tag:work:auth", seen, StringComparer.Ordinal);
    }
}
