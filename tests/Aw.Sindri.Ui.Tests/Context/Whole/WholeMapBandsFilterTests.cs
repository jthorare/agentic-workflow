using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>Every node sits inside its band, whatever the map shows (#1096): the filtered map and the overview with features opened beside it.</summary>
[Lane(TestLane.Unit)]
public sealed class WholeMapBandsFilterTests
{
    private static readonly string[] _kinds = ["record", "decision", "feature", "surface", "action", "thing", "flow", "code", "mystery"];

    private static void AssertInBands(WholeMapGeometry geometry) =>
        Assert.All(geometry.Nodes, node =>
        {
            var band = Assert.Single(geometry.Bands, b => b.Band == NodeBands.Of(node.Kind));
            Assert.InRange(node.Rect.Y, band.Top, band.Top + band.Height);
            Assert.InRange(node.Rect.Bottom, band.Top, band.Top + band.Height);
        });

    [Fact]
    public void A_filtered_map_keeps_every_kept_node_inside_a_kept_band()
    {
        var areas = new[] { "a", "b" }.Select(a => new WholeArea(a, a, "trusted", 1, new Dictionary<string, int>(StringComparer.Ordinal), Unassigned: false)).ToList();
        var nodes = _kinds.SelectMany((k, i) => new[] { "a", "b" }.Select(a => new WholeNode(a + ":" + i.ToString(CultureInfo.InvariantCulture), k, k, a, [], "trusted", "human", 1, Confirmed: true))).ToList();
        var geometry = WholeMapLayout.Place(WholeMapLayoutTests.Draft("nodes-small") with { Areas = areas, AreaEdges = [], Nodes = nodes, Edges = [] });
        var keep = nodes.Where((_, i) => i % 3 == 0).Select(n => n.Key).ToHashSet(StringComparer.Ordinal);

        var shown = WholeMapFilter.Keep(geometry, keep);

        Assert.Equal(keep.Count, shown.Nodes.Count);
        AssertInBands(shown);
    }

    [Fact]
    public void An_overview_with_features_opened_beside_it_keeps_every_node_inside_its_band()
    {
        var overview = WholeMapLayoutTests.Draft("areas-over-budget");
        var opened = WholeMapLayoutTests.Draft("area-open");

        var geometry = WholeMapLayout.PlaceMixed(overview, [opened]);

        Assert.NotEmpty(geometry.Nodes);
        AssertInBands(geometry);
    }
}
