using Avalonia;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Views.Controls;

namespace Aw.Sindri.Ui.Tests.Controls;

/// <summary>The pan and zoom math of the context map (F3 spec 1 §2): the 62 % to 100 % clamp, zoom around the cursor, the fit, the pan into view.</summary>
[Lane(TestLane.Unit)]
public sealed class ZoomViewTests
{
    private static readonly Rect _world = new(0, 0, 1360, 872);

    [Theory]
    [InlineData(0.1, 0.62)]
    [InlineData(0.62, 0.62)]
    [InlineData(0.8, 0.8)]
    [InlineData(1.0, 1.0)]
    [InlineData(2.5, 1.0)]
    public void The_scale_is_clamped_between_62_and_100_percent(double wanted, double expected) =>
        Assert.Equal(expected, ZoomView.Clamp(wanted), 6);

    [Fact]
    public void Zooming_keeps_the_world_point_under_the_cursor_still()
    {
        var view = new ZoomView(1, 30, 20);
        var cursor = new Point(400, 300);
        var before = view.ToWorld(cursor);

        var zoomed = view.ZoomAt(cursor, 0.8);

        Assert.Equal(0.8, zoomed.Scale, 6);
        var after = zoomed.ToWorld(cursor);
        Assert.Equal(before.X, after.X, 6);
        Assert.Equal(before.Y, after.Y, 6);
    }

    [Fact]
    public void Zooming_stops_at_both_ends_of_the_range()
    {
        var view = new ZoomView(1, 0, 0);
        for (var i = 0; i < 40; i++)
        {
            view = view.ZoomAt(new Point(200, 200), 1 / 1.2);
        }

        Assert.Equal(0.62, view.Scale, 6);
        Assert.Equal(0.62, view.ZoomAt(new Point(200, 200), 0.5).Scale, 6);
        Assert.Equal(1.0, view.ZoomAt(new Point(200, 200), 100).Scale, 6);
    }

    [Fact]
    public void A_clamped_zoom_does_not_move_the_map()
    {
        var view = new ZoomView(1, 30, 20);

        Assert.Equal(view, view.ZoomAt(new Point(500, 100), 1.5));
    }

    [Theory]
    [InlineData(1360, 872, 1.0, 0, 0)]
    [InlineData(1800, 1200, 1.0, 220, 164)]
    [InlineData(1100, 700, 0.8028, 4.0, 0)]
    public void Fit_shows_the_whole_world_and_centres_what_is_smaller_than_the_stage(double width, double height, double scale, double x, double y)
    {
        var fit = ZoomView.Fit(new Size(width, height), _world);

        Assert.Equal(scale, fit.Scale, 3);
        Assert.Equal(x, fit.X, 0);
        Assert.Equal(y, fit.Y, 0);
    }

    [Fact]
    public void Fit_on_a_stage_too_small_for_the_floor_starts_at_the_left_edge_and_leaves_the_right_side_to_pan()
    {
        var fit = ZoomView.Fit(new Size(800, 400), _world);

        Assert.Equal(0.62, fit.Scale, 6);
        Assert.Equal(0, fit.X);
        Assert.True((_world.Width * fit.Scale) > 800, "the world is wider than the stage at the floor, so the right side pans");
    }

    [Fact]
    public void Fit_of_an_empty_stage_or_world_is_the_identity()
    {
        Assert.Equal(ZoomView.Identity, ZoomView.Fit(new Size(0, 0), _world));
        Assert.Equal(ZoomView.Identity, ZoomView.Fit(new Size(800, 600), new Rect(0, 0, 0, 0)));
    }

    [Fact]
    public void Panning_moves_the_offset_and_not_the_scale()
    {
        var panned = new ZoomView(0.8, 10, 10).PanBy(-25, 40);

        Assert.Equal(new ZoomView(0.8, -15, 50), panned);
    }

    [Fact]
    public void Dragging_the_map_away_stops_with_120_px_of_it_on_the_stage()
    {
        var stage = new Size(800, 600);
        var far = new ZoomView(1, 0, 0).PanBy(5000, 5000).Kept(stage, _world);
        var back = new ZoomView(1, 0, 0).PanBy(-5000, -5000).Kept(stage, _world);

        Assert.Equal(new ZoomView(1, 680, 480), far);
        Assert.Equal(new ZoomView(1, 120 - 1360, 120 - 872), back);
    }

    [Fact]
    public void A_view_with_the_map_on_the_stage_is_kept_as_it_is()
    {
        var view = new ZoomView(0.7, -200, 30);

        Assert.Equal(view, view.Kept(new Size(900, 600), _world));
    }

    [Fact]
    public void Revealing_a_rectangle_already_in_view_changes_nothing()
    {
        var view = new ZoomView(1, 0, 0);

        Assert.Equal(view, view.Reveal(new Rect(200, 100, 250, 120), new Size(1000, 700)));
    }

    [Fact]
    public void Revealing_a_rectangle_under_the_inspector_pans_it_clear_of_the_inspector_plus_the_margin()
    {
        var view = new ZoomView(1, 0, 0);
        var stage = new Size(1200, 700);

        var revealed = view.Reveal(new Rect(800, 100, 250, 120), stage, rightInset: 480);

        // right edge 1050 must end at 1200 - 480 - 24 = 696
        Assert.Equal(-354, revealed.X, 6);
        Assert.Equal(0, revealed.Y, 6);
    }

    [Fact]
    public void Revealing_a_rectangle_off_the_left_or_top_brings_it_to_the_margin()
    {
        var view = new ZoomView(1, -300, -200);

        var revealed = view.Reveal(new Rect(100, 100, 100, 50), new Size(1000, 700));

        Assert.Equal(-300 + (24 - (100 - 300)), revealed.X, 6);
        Assert.Equal(-200 + (24 - (100 - 200)), revealed.Y, 6);
    }

    [Fact]
    public void Revealing_follows_the_scale()
    {
        var view = new ZoomView(0.5, 0, 0);

        // on screen the rectangle spans x 650 to 700; the room ends at 700 - 24 = 676
        var revealed = view.Reveal(new Rect(1300, 0, 100, 100), new Size(700, 500));

        Assert.Equal(-24, revealed.X, 6);
        Assert.Equal(0.5, revealed.Scale, 6);
    }

    [Fact]
    public void A_rectangle_wider_than_the_room_lines_its_left_edge_up_with_the_margin()
    {
        var view = new ZoomView(1, 0, 0);

        var revealed = view.Reveal(new Rect(500, 10, 900, 100), new Size(800, 600));

        Assert.Equal(24 - 500, revealed.X, 6);
    }

    [Fact]
    public void Lerp_is_the_straight_blend_of_scale_and_offset()
    {
        var mid = ZoomView.Lerp(new ZoomView(1, 0, 0), new ZoomView(0.62, 100, 40), 0.5);

        Assert.Equal(0.81, mid.Scale, 6);
        Assert.Equal(50, mid.X, 6);
        Assert.Equal(20, mid.Y, 6);
    }

    private static readonly ZoomRange _wide = new(0.30, 1.50);

    [Fact]
    public void A_stage_can_have_its_own_zoom_range()
    {
        Assert.Equal(0.3, ZoomView.Clamp(0.1, _wide), 6);
        Assert.Equal(1.5, ZoomView.Clamp(9, _wide), 6);
        Assert.Equal(1.5, new ZoomView(1, 0, 0).ZoomAt(new Point(10, 10), 10, _wide).Scale, 6);
        Assert.Equal(0.3, new ZoomView(1, 0, 0).ZoomAt(new Point(10, 10), 0.01, _wide).Scale, 6);
    }

    [Fact]
    public void The_default_range_is_62_to_100_percent() => Assert.Equal(new ZoomRange(0.62, 1.0), ZoomRange.Default);

    [Fact]
    public void Fit_can_scale_up_when_the_range_allows()
    {
        var fit = ZoomView.Fit(new Size(800, 600), new Rect(0, 0, 400, 300), _wide);

        Assert.Equal(1.5, fit.Scale, 6);
        Assert.Equal(100, fit.X, 6);
        Assert.Equal(75, fit.Y, 6);
    }

    [Fact]
    public void Fit_follows_a_world_that_does_not_start_at_the_origin()
    {
        var fit = ZoomView.Fit(new Size(1000, 500), new Rect(-200, -100, 2000, 1000), _wide);

        Assert.Equal(0.5, fit.Scale, 6);
        Assert.Equal(100, fit.X, 6);
        Assert.Equal(50, fit.Y, 6);
        var corner = new Point((-200 * fit.Scale) + fit.X, (-100 * fit.Scale) + fit.Y);
        Assert.Equal(new Point(0, 0), corner);
    }

    [Fact]
    public void Keeping_the_world_on_the_stage_counts_from_where_the_world_starts()
    {
        var stage = new Size(800, 600);
        var world = new Rect(-1000, 0, 1360, 872);

        Assert.Equal(new ZoomView(1, 0, 0), new ZoomView(1, 0, 0).Kept(stage, world));
        var far = new ZoomView(1, 0, 0).PanBy(-5000, 0).Kept(stage, world);
        Assert.Equal(120 - 1360, far.X + (world.X * far.Scale), 6);
    }
}
