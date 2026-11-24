using System.Globalization;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels.Context.Whole;
using Aw.Sindri.Ui.Views.Context.Whole;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>
/// #3998: on the 14-item map (two features, so a narrow world) the Product lane's heading "2 actions · 2 features · 2 screens · 1 thing" is drawn whole,
/// not cut at the old fixed 360 px reserve. Saved under frames/ as whole-lane-heading-*.png when AW_UI_FRAMES=1, before the checks.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class WholeMapLaneHeadingFramesTests : FrameHost
{
    [Theory]
    [InlineData(1280)]
    [InlineData(1000)]
    public Task The_product_lane_heading_is_drawn_whole_on_a_narrow_map(double width) => OnUi(async () =>
    {
        var (window, shell) = await Connected(width: width, height: 900);
        shell.GoTo(PageId.Context);
        await Until(window, () => shell.Context.ShowWhole && window.GetVisualDescendants().OfType<WholeMapView>().Any(v => v.Canvas.Bounds.Width > 0));
        Settle(window);
        Named<WholeMapView>(window, "WholeMap").Canvas.Settle();
        Settle(window);
        Frame(window, string.Create(CultureInfo.InvariantCulture, $"lane-heading-{width:0}"), "whole-");

        var canvas = Named<WholeMapCanvas>(window, "WholeWorld");
        var product = Assert.Single(canvas.Map?.Shown?.Bands.Where(b => b.Band == WholeBand.Product) ?? []);

        Assert.NotEmpty(product.Meta);
        Assert.True(canvas.LaneMetaWhole(WholeBand.Product), $"cut: {product.Meta}");
    });
}
