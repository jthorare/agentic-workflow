using System.Globalization;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Results;
using Aw.Context.Testing.Graph;

namespace Aw.Context.Tests.Lenses;

/// <summary>Spec 2026-10-27 §10.2 and Open question 17: Concept is a product kind, so a pin's lens lists the other pins and tables it reaches, and a feature's lens lists the pins on its table.</summary>
public sealed class PinLensTests
{
    private static LensesView Lens(GraphSnapshot graph, string anchor, int depth) =>
        LensesBuilder.Build(new LensBasis("v1@abc", Draft: false, DateTimeOffset.Parse("2026-10-27T12:00:00Z", CultureInfo.InvariantCulture)), graph, NodeKey.From(anchor), depth).Value;

    private static GraphSnapshot Pinned() => new GraphBuilder()
        .Node("feature:orders", provenance: Provenance.Deterministic).Node("concept:money").Node("concept:people").Node("thing:orders-t").Node("thing:invoices")
        .Edge("feature:orders", EdgeType.Writes, "thing:orders-t", provenance: Provenance.Deterministic)
        .Edge("concept:money", EdgeType.DependsOn, "concept:people", provenance: Provenance.Deterministic)
        .Edge("concept:money", EdgeType.Writes, "thing:invoices", provenance: Provenance.Deterministic)
        .Edge("concept:people", EdgeType.Reads, "thing:orders-t", provenance: Provenance.Deterministic)
        .Build();

    private static string[] Listed(LensesView view) =>
        [.. view.Bands.Product.Neighbors.Select(n => n.Node.Value).Concat(view.Bands.Product.Affects.Select(a => a.Node.Value)).Order(StringComparer.Ordinal)];

    [Fact, Trait("Lane", "unit")]
    public void A_pins_lens_at_depth_one_lists_the_pin_that_depends_on_it_and_the_table_it_reads()
    {
        var listed = Listed(Lens(Pinned(), "concept:people", depth: 1));

        Assert.Contains("concept:money", listed, StringComparer.Ordinal);
        Assert.Contains("thing:orders-t", listed, StringComparer.Ordinal);
        Assert.DoesNotContain("thing:invoices", listed, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_pins_lens_at_depth_two_also_lists_what_the_next_pin_writes()
    {
        var listed = Listed(Lens(Pinned(), "concept:people", depth: 2));

        Assert.Contains("thing:invoices", listed, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_feature_lens_that_reaches_a_table_lists_the_pin_that_reads_it_one_hop_on()
    {
        var listed = Listed(Lens(Pinned(), "feature:orders", depth: 2));

        Assert.Contains("thing:orders-t", listed, StringComparer.Ordinal);
        Assert.Contains("concept:people", listed, StringComparer.Ordinal);
    }
}
