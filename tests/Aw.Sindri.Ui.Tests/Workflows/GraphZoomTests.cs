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

        var twice = GraphZoom.Next(GraphZoom.Next((int)Math.Round(fit.Zoom * 100))) / 100.0;   // 98 -> 100 -> 110

        Assert.True(GraphZoom.Scrolls(fit.Width, fit.Height, twice, clip));   // shown after two + clicks
    }

    [Fact]
    public void Scroll_extent_adds_map_height_plus_16_at_the_bottom_while_the_map_shows()
    {
        Assert.Equal(1000, GraphZoom.ExtentHeight(1000, 1.0, mapShown: false));
        Assert.Equal(1144, GraphZoom.ExtentHeight(1000, 1.0, mapShown: true));   // 128 + 16 = 144, bottom only
    }
}
