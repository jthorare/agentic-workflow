using System.Globalization;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels.Context.Whole;
using Aw.Sindri.Ui.Views.Context.Whole;
using Aw.Sindri.Ui.Views.Controls;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>
/// #3998: on the 14-item map (two features, so a narrow world) the Product lane's heading "2 actions · 2 features · 2 screens · 1 thing" is drawn whole,
/// not cut at the old fixed 360 px reserve. #4305 tested dots mode at the lowest zoom; since #3674 the whole map never zooms out past 50%,
/// above dots mode, so at the lowest zoom the heading keeps its counts and they are drawn whole.
/// Saved under frames/ as whole-lane-heading-*.png when AW_UI_FRAMES=1, before the checks.
/// The width checks need real drawing (#4324): the headless runner without AW_UI_FRAMES=1 shapes text with stand-in metrics, about 15% wider
/// (514.8 against 449 of room on this map), so there they only check what is drawn, not how wide.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class WholeMapLaneHeadingFramesTests : FrameHost
{
    private static readonly bool _draws = string.Equals(Environment.GetEnvironmentVariable("AW_UI_FRAMES"), "1", StringComparison.Ordinal);

    private async Task<(Window Window, WholeMapView Whole)> Open(double width)
    {
        var (window, shell) = await Connected(width: width, height: 900);
        shell.GoTo(PageId.Context);
        await Until(window, () => shell.Context.ShowWhole && window.GetVisualDescendants().OfType<WholeMapView>().Any(v => v.Canvas.Bounds.Width > 0));
        Settle(window);
        var whole = Named<WholeMapView>(window, "WholeMap");
        whole.Canvas.Settle();
        Settle(window);
        return (window, whole);
    }

    [Theory]
    [InlineData(1280)]
    [InlineData(1000)]
    public Task The_product_lane_heading_is_drawn_whole_on_a_narrow_map(double width) => OnUi(async () =>
    {
        var (window, _) = await Open(width);
        Frame(window, string.Create(CultureInfo.InvariantCulture, $"lane-heading-{width:0}"), "whole-");

        var canvas = Named<WholeMapCanvas>(window, "WholeWorld");
        var product = Assert.Single(canvas.Map?.Shown?.Bands.Where(b => b.Band == WholeBand.Product) ?? []);

        Assert.NotEmpty(product.Meta);
        Assert.True(canvas.LaneMetaDrawn(WholeBand.Product), "counts not drawn at the default zoom");
        Assert.True(!_draws || canvas.LaneMetaWhole(WholeBand.Product), string.Create(CultureInfo.InvariantCulture, $"cut: {product.Meta} (shown world {canvas.Map?.Shown?.World.Width:0}, whole world {canvas.Map?.Geometry?.World.Width:0}, scale {canvas.Scale:0.###}, {canvas.LaneMetaNumbers(WholeBand.Product)})"));
    });

    [Theory]
    [InlineData(1280)]
    [InlineData(1000)]
    public Task Zoomed_out_to_the_floor_the_product_lane_heading_keeps_its_counts_whole(double width) => OnUi(async () =>
    {
        var (window, whole) = await Open(width);
        whole.Canvas.SetView(ZoomView.Identity with { Scale = whole.Canvas.MinZoom });
        Settle(window);
        Frame(window, string.Create(CultureInfo.InvariantCulture, $"lane-heading-floor-{width:0}"), "whole-");

        var canvas = Named<WholeMapCanvas>(window, "WholeWorld");
        var product = Assert.Single(canvas.Map?.Shown?.Bands.Where(b => b.Band == WholeBand.Product) ?? []);

        Assert.False(WholeMapViewModel.UsesDots(canvas.Scale), string.Create(CultureInfo.InvariantCulture, $"dots mode at the zoom floor {canvas.Scale}"));
        Assert.NotEmpty(product.Meta);
        Assert.True(canvas.LaneMetaDrawn(WholeBand.Product), $"counts not drawn at the zoom floor: {product.Meta}");
        Assert.True(!_draws || canvas.LaneMetaWhole(WholeBand.Product), string.Create(CultureInfo.InvariantCulture, $"cut at the zoom floor {canvas.Scale:0.###}: {product.Meta} ({canvas.LaneMetaNumbers(WholeBand.Product)})"));
        Assert.True(!_draws || canvas.LaneTitleWhole(WholeBand.Product), string.Create(CultureInfo.InvariantCulture, $"title cut at the zoom floor {canvas.Scale:0.###}"));
    });
}
