using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>The Why drill shows the product structure under what the Why band explains, and says so when none is linked (#1701).</summary>
[Lane(TestLane.Unit)]
public sealed class WhyBandProductContextTests
{
    private static WholeMapGeometry Place(string[] keys, (string From, string To, string Type)[] links)
    {
        var map = WholeMapLayoutTests.Draft("nodes-small");
        var area = map.Areas[0].Key;
        return WholeMapLayout.Place(map with
        {
            Areas = [map.Areas[0]],
            AreaEdges = [],
            Nodes = [.. keys.Select(k => new WholeNode(k, k, k[..k.IndexOf(':', StringComparison.Ordinal)], area, [], "trusted", "human", 1, Confirmed: true))],
            Edges = [.. links.Select(l => new WholeEdge(l.From, l.To, l.Type, $"edge:{l.From}>{l.To}", "human", 1, Confirmed: true))],
        });
    }

    private static WholeMapGeometry Product() => Place(
        ["decision:why-swaps", "decision:why-goal", "feature:swaps", "thing:staffing-goal", "surface:swap-board", "surface:goals", "surface:reports", "action:offer-shift", "action:set-goal", "action:export", "code:swaps.ts"],
        [
            ("decision:why-swaps", "feature:swaps", "decides"),
            ("surface:swap-board", "feature:swaps", "part-of"),
            ("action:offer-shift", "surface:swap-board", "on"),
            ("decision:why-goal", "thing:staffing-goal", "decides"),
            ("action:set-goal", "thing:staffing-goal", "writes"),
            ("action:set-goal", "surface:goals", "on"),
            ("code:swaps.ts", "feature:swaps", "implements"),
            ("action:export", "surface:reports", "on"),
        ]);

    private static string[] Keys(WholeMapGeometry geometry) => [.. geometry.Nodes.Select(n => n.Key)];

    private static string[] Types(WholeMapGeometry geometry) => [.. geometry.Edges.Select(e => e.Type)];

    [Fact]
    public void Why_drill_shows_the_surfaces_and_actions_under_a_decided_feature()
    {
        var shown = WholeMapBandDrill.Cross(Product(), WholeBand.Intent);

        foreach (var key in new[] { "feature:swaps", "surface:swap-board", "action:offer-shift" })
        {
            Expect.True(Keys(shown).Contains(key), $"{key} is drawn");
        }

        foreach (var type in new[] { "decides", "part-of", "on" })
        {
            Expect.True(Types(shown).Contains(type), $"a {type} link is drawn");
        }
    }

    [Fact]
    public void Why_drill_shows_the_actions_and_pages_that_touch_a_decided_thing()
    {
        var shown = WholeMapBandDrill.Cross(Product(), WholeBand.Intent);

        foreach (var key in new[] { "thing:staffing-goal", "action:set-goal", "surface:goals" })
        {
            Expect.True(Keys(shown).Contains(key), $"{key} is drawn");
        }

        Expect.True(Types(shown).Contains("writes"), "a writes link is drawn");
    }

    [Fact]
    public void Why_drill_keeps_code_and_unrelated_product_out()
    {
        var shown = WholeMapBandDrill.Cross(Product(), WholeBand.Intent);

        foreach (var key in new[] { "code:swaps.ts", "surface:reports", "action:export" })
        {
            Expect.True(!Keys(shown).Contains(key), $"{key} stays out");
        }

        Expect.True(shown.Nodes.All(n => NodeBands.Of(n.Kind) is WholeBand.Intent or WholeBand.Product), "every shown node is Why or Product");
    }

    [Fact]
    public void Why_drill_says_when_no_product_evidence_is_linked()
    {
        var lonely = Place(
            ["decision:lonely", "record:r", "surface:reports", "action:export"],
            [("decision:lonely", "record:r", "quotes"), ("action:export", "surface:reports", "on")]);

        var shown = WholeMapBandDrill.Cross(lonely, WholeBand.Intent);

        Expect.True(shown.Bands.Any(b => b.Band == WholeBand.Product), "the Product band stays");
        Expect.Equal(Labels.ContextDrillWhyNoProduct, shown.Bands.Single(b => b.Band == WholeBand.Product).Meta);
    }
}
