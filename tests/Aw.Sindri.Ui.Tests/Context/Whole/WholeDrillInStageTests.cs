using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>An opened area's stage (#3674, decisions 1 and 4): the canvas up to 40 nodes, the list above, and the layout runs after the answer is drawn.</summary>
[Lane(TestLane.Unit)]
public sealed class WholeDrillInStageTests
{
    private static readonly TimeZoneInfo _utc = TimeZoneInfo.Utc;

    private static WholeMapView Area(int nodes)
    {
        var draft = WholeMapLayoutTests.Draft("area-open");
        var key = draft.Area!.Key;
        var rows = Enumerable.Range(0, nodes).Select(i => new WholeNode(string.Create(CultureInfo.InvariantCulture, $"surface:screen/s{i:000}"), string.Create(CultureInfo.InvariantCulture, $"Screen {i:000}"), "surface", key, [], "trusted", "deterministic", 1, Confirmed: false)).ToList();
        return draft with { Nodes = rows, Edges = [], Area = draft.Area with { Counts = draft.Area.Counts with { Nodes = nodes } } };
    }

    [Theory]
    [InlineData(30, "Canvas")]
    [InlineData(40, "Canvas")]
    [InlineData(41, "List")]
    [InlineData(50, "List")]
    public void An_opened_area_is_a_canvas_up_to_forty_nodes_and_a_list_above(int nodes, string stage)
    {
        var map = new WholeMapViewModel(() => _utc);
        map.Show(WholeMapLayoutTests.Draft("areas-over-budget"));

        map.ShowArea(Area(nodes));

        Assert.Equal(stage, map.Stage.ToString());
    }

    [Fact]
    public async Task A_click_draws_the_list_from_the_answer_first_and_the_canvas_after_the_layout()
    {
        var map = new WholeMapViewModel(() => _utc);
        map.Show(WholeMapLayoutTests.Draft("areas-over-budget"));

        map.ShowArea(Area(30));

        Assert.Null(map.Geometry);   // why: the layout runs off the UI thread, so the answer is on screen before it
        await map.AreaLaidOut;
        Assert.Equal(30, map.Geometry!.Nodes.Count);
    }

    [Fact]
    public async Task A_list_stage_never_lays_out_a_canvas()
    {
        var map = new WholeMapViewModel(() => _utc);
        map.Show(WholeMapLayoutTests.Draft("areas-over-budget"));

        map.ShowArea(Area(50));
        await map.AreaLaidOut;

        Assert.Null(map.Geometry);
    }
}
