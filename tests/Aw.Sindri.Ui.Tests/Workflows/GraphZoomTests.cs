using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>The zoom strip's steps and the scroll rules of the workflow canvas (#3684).</summary>
[Lane(TestLane.Unit)]
public sealed class GraphZoomTests
{
    [Theory]
    [InlineData(98, 100)]
    [InlineData(100, 110)]
    [InlineData(110, 125)]
    [InlineData(200, 200)]
    public void Plus_goes_to_the_next_step(int from, int to) => Assert.Equal(to, GraphZoom.Next(from));

    [Theory]
    [InlineData(98, 90)]
    [InlineData(100, 90)]
    [InlineData(25, 25)]
    public void Minus_goes_to_the_next_step_down(int from, int to) => Assert.Equal(to, GraphZoom.Previous(from));

    [Fact]
    public void Minimap_shows_only_when_the_canvas_scrolls()
    {
        var fit = GraphFit.Compute(GraphFitTests.Release(0), GraphFitTests.CanvasWidth, GraphFitTests.CanvasHeight, 1.0);
        var clip = (GraphFitTests.CanvasWidth, GraphFitTests.CanvasHeight);

        Assert.False(GraphZoom.Scrolls(fit.Width, fit.Height, fit.Zoom, clip));   // hidden at Fit with no overflow

        var twice = GraphZoom.Next(GraphZoom.Next((int)Math.Round(fit.Zoom * 100, MidpointRounding.AwayFromZero))) / 100.0;   // 98 -> 100 -> 110

        Assert.True(GraphZoom.Scrolls(fit.Width, fit.Height, twice, clip));   // shown after two + clicks
    }

    [Fact]
    public void Scroll_extent_adds_map_height_plus_16_at_the_bottom_while_the_map_shows()
    {
        Assert.Equal(1000, GraphZoom.ExtentHeight(1000, 1.0, mapShown: false));
        Assert.Equal(1144, GraphZoom.ExtentHeight(1000, 1.0, mapShown: true));   // 128 + 16 = 144, bottom only
    }

    // canvas-40: the floor binds (86 %), the world is as wide as the canvas and taller than it, so the canvas scrolls down only.
    private static readonly (double Width, double Height) _clip = (GraphFitTests.CanvasWidth, GraphFitTests.CanvasHeight);

    [Fact]
    public void Pan_cannot_go_past_the_extent_bottom_and_the_last_row_sits_above_the_map_at_the_end()
    {
        var fit = GraphFit.Compute(GraphFitTests.Release(21), GraphFitTests.CanvasWidth, GraphFitTests.CanvasHeight, 1.0);

        var (_, y) = GraphZoom.ClampPan((0, -100000), (fit.Width, fit.Height), fit.Zoom, _clip);

        Assert.Equal(GraphFitTests.CanvasHeight - (fit.Height * fit.Zoom) - 144, y, 3);
        Assert.True((fit.Height * fit.Zoom) + y <= GraphFitTests.CanvasHeight - 144 + 0.001, "the last row sits under the map at the end of the scroll");
    }

    [Fact]
    public void Fit_never_scrolls_sideways_when_the_map_shows()
    {
        var fit = GraphFit.Compute(GraphFitTests.Release(21), GraphFitTests.CanvasWidth, GraphFitTests.CanvasHeight, 1.0);

        var (x, _) = GraphZoom.ClampPan((-50, 0), (fit.Width, fit.Height), fit.Zoom, _clip);

        Assert.Equal(0, x, 3);   // hScroll 0: the world is no wider than the canvas
    }

    [Fact]
    public void Pan_cannot_leave_the_top_left() =>
        Assert.Equal((0, 0), GraphZoom.ClampPan((30, 40), (3000, 3000), 1.0, _clip));

    [Theory]
    [InlineData(900, 140, 16, 744)]
    [InlineData(500, 140, 16, 344)]
    [InlineData(400, 140, 16, 320)]
    public void The_stage_fills_the_viewport_under_its_top_and_never_goes_under_320(double viewport, double top, double gap, double expected) =>
        Assert.Equal(expected, GraphZoom.StageHeight(viewport, top, gap), 3);
}
