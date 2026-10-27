using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>Drilling into a band of a concept's map (#1289): cross-band links first, then the band's own links; the bands that stay say what is drawn in them.</summary>
[Lane(TestLane.Unit)]
public sealed class WholeMapBandDrillTests
{
    /// <summary>The Self scheduling concept of the concepts fixture, placed.</summary>
    internal static WholeMapGeometry Concept()
    {
        var map = WholeMapLayoutTests.Draft("nodes-small");
        var area = map.Areas[0].Key;
        WholeNode Node(string key, string kind) => new(key, key, kind, area, [], "trusted", "human", 1, Confirmed: true);
        WholeEdge Edge(string from, string to, string type) => new(from, to, type, $"edge:{from}>{to}", "human", 1, Confirmed: true);
        return WholeMapLayout.Place(map with
        {
            Areas = [map.Areas[0]],
            AreaEdges = [],
            Nodes = [Node("decision:d", "decision"), Node("feature:shifts", "feature"), Node("feature:swaps", "feature"), Node("surface:screen", "surface"), Node("action:pick", "action"), Node("code:shifts.ts", "code")],
            Edges =
            [
                Edge("decision:d", "feature:swaps", "decides"),
                Edge("code:shifts.ts", "feature:shifts", "implements"),
                Edge("surface:screen", "feature:shifts", "part-of"),
                Edge("action:pick", "surface:screen", "on"),
            ],
        });
    }

    private static string[] Keys(WholeMapGeometry geometry) => [.. geometry.Nodes.Select(n => n.Key).Order(StringComparer.Ordinal)];

    private static string Meta(WholeMapGeometry geometry, WholeBand band) => geometry.Bands.Single(b => b.Band == band).Meta;

    [Fact]
    public void Why_across_keeps_the_decision_and_what_it_decides_and_only_that_link()
    {
        var shown = WholeMapBandDrill.Cross(Concept(), WholeBand.Intent);

        Expect.Sequence(["decision:d", "feature:swaps"], Keys(shown));
        Expect.Sequence(["decides"], shown.Edges.Select(e => e.Type));
        Expect.Sequence([WholeBand.Intent, WholeBand.Product], shown.Bands.Select(b => b.Band));
        Expect.Equal(Labels.ContextWholeKindCount("decision", 1), Meta(shown, WholeBand.Intent));
        Expect.Equal(Labels.ContextWholeKindCount("feature", 1), Meta(shown, WholeBand.Product));
    }

    [Fact]
    public void Product_across_keeps_the_features_with_a_link_out_and_the_nodes_on_the_other_ends()
    {
        var shown = WholeMapBandDrill.Cross(Concept(), WholeBand.Product);

        Expect.Sequence(["code:shifts.ts", "decision:d", "feature:shifts", "feature:swaps"], Keys(shown));
        Expect.Sequence(["decides", "implements"], shown.Edges.Select(e => e.Type).Order(StringComparer.Ordinal));
        Expect.Equal(Labels.ContextWholeKindCount("feature", 2), Meta(shown, WholeBand.Product));
    }

    [Fact]
    public void Code_across_keeps_the_file_and_the_feature_it_implements()
    {
        var shown = WholeMapBandDrill.Cross(Concept(), WholeBand.Code);

        Expect.Sequence(["code:shifts.ts", "feature:shifts"], Keys(shown));
        Expect.Sequence([WholeBand.Product, WholeBand.Code], shown.Bands.Select(b => b.Band));
    }

    [Fact]
    public void Why_inside_keeps_the_decision_and_no_links()
    {
        var shown = WholeMapBandDrill.Own(Concept(), WholeBand.Intent);

        Expect.Sequence(["decision:d"], Keys(shown));
        Expect.Equal(0, shown.Edges.Count);
        Expect.Sequence([WholeBand.Intent], shown.Bands.Select(b => b.Band));
    }

    [Fact]
    public void Product_inside_keeps_the_product_nodes_and_the_links_between_them()
    {
        var shown = WholeMapBandDrill.Own(Concept(), WholeBand.Product);

        Expect.Sequence(["action:pick", "feature:shifts", "feature:swaps", "surface:screen"], Keys(shown));
        Expect.Sequence(["on", "part-of"], shown.Edges.Select(e => e.Type).Order(StringComparer.Ordinal));
        Expect.Sequence([WholeBand.Product], shown.Bands.Select(b => b.Band));
    }

    [Fact]
    public void The_cut_keeps_the_places_nodes_had()
    {
        var whole = Concept();

        var shown = WholeMapBandDrill.Own(whole, WholeBand.Product);

        Expect.True(shown.Nodes.All(n => whole.Nodes.Contains(n)));
        Expect.Equal(whole.World, shown.World);
    }
}
