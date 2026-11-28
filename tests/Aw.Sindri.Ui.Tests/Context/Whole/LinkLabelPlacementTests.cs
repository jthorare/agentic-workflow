using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>#3678: a link label is drawn whole or not at all, never under a node. It needs a free stretch of the label plus 16 px between the two nodes.</summary>
[Lane(TestLane.Unit)]
public sealed class LinkLabelPlacementTests
{
    private static readonly WholeRect _left = new(0, 100, 100, 28);

    [Fact]
    public void A_wide_enough_horizontal_gap_holds_the_label_centred_between_the_nodes()
    {
        var right = new WholeRect(200, 100, 100, 28);

        var placed = LinkLabelPlacer.Place(_left, right, width: 60, height: 14, [_left, right]);

        _ = Assert.NotNull(placed);
        Assert.Equal(150, placed.Value.CenterX, 3);
        Assert.Equal(114, placed.Value.CenterY, 3);
        Assert.False(placed.Value.Intersects(_left) || placed.Value.Intersects(right));
    }

    [Theory]
    [InlineData(76, true)]
    [InlineData(75.9, false)]
    [InlineData(20, false)]
    public void The_horizontal_gap_must_be_at_least_the_label_width_plus_sixteen(double gap, bool drawn)
    {
        var right = new WholeRect(100 + gap, 100, 100, 28);

        Assert.Equal(drawn, LinkLabelPlacer.Place(_left, right, width: 60, height: 14, [_left, right]) is not null);
    }

    [Theory]
    [InlineData(30, true)]
    [InlineData(29.9, false)]
    [InlineData(16, false)]
    public void The_vertical_gap_must_be_at_least_the_label_height_plus_sixteen(double gap, bool drawn)
    {
        var top = new WholeRect(0, 0, 100, 28);
        var below = new WholeRect(0, 28 + gap, 100, 28);

        Assert.Equal(drawn, LinkLabelPlacer.Place(top, below, width: 60, height: 14, [top, below]) is not null);
    }

    [Fact]
    public void A_vertical_link_puts_the_label_inside_its_gap()
    {
        var top = new WholeRect(0, 0, 100, 28);
        var below = new WholeRect(0, 78, 100, 28);

        var placed = LinkLabelPlacer.Place(top, below, width: 60, height: 14, [top, below]);

        _ = Assert.NotNull(placed);
        Assert.InRange(placed.Value.Y, 28, 78 - 14);
    }

    [Fact]
    public void A_stretch_with_a_third_node_on_it_draws_no_label()
    {
        var right = new WholeRect(300, 100, 100, 28);
        var between = new WholeRect(140, 90, 100, 50);

        Assert.Null(LinkLabelPlacer.Place(_left, right, width: 60, height: 14, [_left, right, between]));
    }

    [Fact]
    public void Nodes_that_overlap_have_no_free_stretch()
    {
        var onTop = new WholeRect(50, 110, 100, 28);

        Assert.Null(LinkLabelPlacer.Place(_left, onTop, width: 60, height: 14, [_left, onTop]));
    }
}
