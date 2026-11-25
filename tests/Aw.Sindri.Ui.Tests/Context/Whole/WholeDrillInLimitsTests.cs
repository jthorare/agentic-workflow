using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>The drill-in's limits (#3674, meta's decisions 1 and 5): at most 40 nodes on the canvas, the Core band's columns by window width, the bands by node kind, no zoom below 50%.</summary>
[Lane(TestLane.Unit)]
public sealed class WholeDrillInLimitsTests
{
    [Theory]
    [InlineData(0, "Canvas")]
    [InlineData(40, "Canvas")]
    [InlineData(41, "List")]
    [InlineData(6891, "List")]
    public void The_canvas_draws_forty_nodes_at_most_and_above_that_the_stage_is_the_list(int nodes, string stage) =>
        Assert.Equal(stage, WholeDrillIn.StageFor(nodes).ToString());

    [Theory]
    [InlineData(1920, 2)]
    [InlineData(1390, 2)]
    [InlineData(1389, 1)]
    [InlineData(1280, 1)]
    [InlineData(1138, 1)]
    [InlineData(1137, 0)]
    [InlineData(1000, 0)]
    public void The_core_band_has_two_columns_from_1390_one_from_1138_and_below_that_the_list(double width, int columns) =>
        Assert.Equal(columns, WholeDrillIn.CoreColumns(width));

    [Theory]
    [InlineData("route", "Entry")]
    [InlineData("job", "Entry")]
    [InlineData("middleware", "Entry")]
    [InlineData("table", "Data")]
    [InlineData("event", "Data")]
    [InlineData("migration", "Data")]
    [InlineData("code", "Core")]
    [InlineData("feature", "Core")]
    [InlineData("thing", "Core")]
    public void A_node_kind_goes_to_its_band_and_anything_else_to_core(string kind, string band) =>
        Assert.Equal(band, WholeDrillIn.BandOf(kind).ToString());

    [Theory]
    [InlineData("AdvanceReportRequest", "Requests")]
    [InlineData("AdvanceReportResponse", "Responses")]
    [InlineData("AdvanceReportService", "Services")]
    [InlineData("SweepRule", "Services")]
    [InlineData("Aw.BugFix.Features", "Services")]
    public void A_core_node_sits_in_requests_services_and_rules_or_responses(string label, string group) =>
        Assert.Equal(group, WholeDrillIn.CoreGroupOf(label).ToString());

    [Theory]
    [InlineData(5.0)]
    [InlineData(0.6)]
    [InlineData(0.1)]
    [InlineData(0.001)]
    public void The_map_never_zooms_out_below_half_size(double fit) =>
        Assert.Equal(WholeDrillIn.MinZoom, WholeMapViewModel.ZoomLimits(fit).Min);

    [Fact]
    public void The_smallest_zoom_is_fifty_percent() => Assert.Equal(0.5, WholeDrillIn.MinZoom);

    [Fact]
    public void Nodes_in_a_band_keep_a_gap_of_sixteen_px() => Assert.Equal(16, WholeMapLayout.NodeGap);
}
