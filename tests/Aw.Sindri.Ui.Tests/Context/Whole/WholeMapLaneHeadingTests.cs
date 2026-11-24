using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>A lane's heading runs to the lane's right end (#3998): a narrow map no longer cuts "2 actions · 2 features · 2 screens · 1 th…" while the lane has room.</summary>
[Lane(TestLane.Unit)]
public sealed class WholeMapLaneHeadingTests
{
    [Fact]
    public void A_heading_gets_the_lane_from_where_it_starts_to_the_right_end_less_the_margin() =>
        Assert.Equal(600 - 80 - (WholeMapLayout.Margin / 2), WholeMapLayout.LaneMetaWidth(600, 80));

    [Fact]
    public void On_the_two_feature_map_the_heading_gets_more_than_the_old_fixed_reserve_left()
    {
        var world = WholeMapLayout.Place(WholeMapLayoutTests.Draft("nodes-small")).World.Width;
        var width = WholeMapLayout.LaneMetaWidth(world, 80);

        Assert.True(width > world - 360, string.Create(CultureInfo.InvariantCulture, $"{width} <= {world - 360}"));
    }

    [Fact]
    public void The_two_feature_map_is_never_narrower_than_the_world_floor()
    {
        var world = WholeMapLayout.Place(WholeMapLayoutTests.Draft("nodes-small")).World.Width;

        Assert.True(world >= WholeMapLayout.MinWorldWidth, string.Create(CultureInfo.InvariantCulture, $"{world} < {WholeMapLayout.MinWorldWidth}"));
    }

    [Fact]
    public void A_lane_too_narrow_for_its_heading_still_gives_80() => Assert.Equal(80, WholeMapLayout.LaneMetaWidth(100, 90));
}
