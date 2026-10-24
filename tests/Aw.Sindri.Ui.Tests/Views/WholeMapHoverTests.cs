using Avalonia;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Context.Whole;
using Aw.Sindri.Ui.Views.Context.Whole;
using Aw.Sindri.Ui.Views.Controls;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>The whole map's hover and click, through the real pointer on the real window: the mockup's dimming, and a click that pins it. Saved under frames/ as map-hover-*.png when AW_UI_FRAMES=1.</summary>
[Lane(TestLane.Integration)]
public sealed class WholeMapHoverTests : FrameHost
{
    private async Task<(Avalonia.Controls.Window Window, WholeMapView Whole, WholeMapViewModel Map)> Open(double width, string golden)
    {
        Server.WholeMapAnswer = q => q.Area is null ? new(System.Net.HttpStatusCode.OK, GoldenFiles.Api("context-whole", golden)) : ContextAnswers.WholeMap(q);
        var (window, shell) = await Connected(width: width, height: 900);
        shell.GoTo(PageId.Context);
        await Until(window, () => shell.Context.ShowWhole && window.GetVisualDescendants().OfType<WholeMapView>().Any(v => v.Canvas.Bounds.Width > 0));
        Settle(window);
        var whole = Named<WholeMapView>(window, "WholeMap");
        whole.Canvas.Settle();
        return (window, whole, shell.Context.Whole);
    }

    // Brings a key's box into view at full size, and returns where its centre is in the window.
    private static Point Centre(Avalonia.Controls.Window window, WholeMapView whole, WholeRect rect)
    {
        whole.Canvas.SetView(ZoomView.Identity with { X = 200 - rect.CenterX, Y = 200 - rect.CenterY });
        window.UpdateLayout();
        return whole.World.TranslatePoint(new Point(rect.CenterX, rect.CenterY), window)!.Value;
    }

    [Fact]
    public Task Hovering_a_tile_dims_the_others_and_a_click_pins_it_until_Esc() => OnUi(async () =>
    {
        var (window, whole, map) = await Open(1440, "areas-over-budget");
        var tile = map.Geometry!.Tiles[0];
        var at = Centre(window, whole, tile.Rect);

        window.MouseMove(at);
        await Until(window, () => whole.World.DimStrength >= 1);
        Assert.Equal(tile.Key, map.Focus);
        Frame(window, "map-hover-tiles-1440", "s1-");

        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
        Assert.Equal(tile.Key, map.Pinned);
        window.MouseMove(new Point(2, 2));
        await Until(window, () => whole.World.DimStrength >= 1);   // the pin keeps it dimmed after the pointer left
        Assert.Equal(tile.Key, map.Pinned);

        Named<Avalonia.Controls.TextBox>(window, "AnchorBox").RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
        await Until(window, () => whole.World.DimStrength <= 0);
        Assert.Null(map.Pinned);
    });

    [Fact]
    public Task Hovering_a_node_on_an_opened_feature_dims_the_rest() => OnUi(async () =>
    {
        var (window, whole, map) = await Open(900, "nodes-small");
        var node = map.Geometry!.Edges.Select(e => map.Geometry.Nodes.First(n => n.Key == e.From)).First();
        var at = Centre(window, whole, node.Rect);

        window.MouseMove(at);
        await Until(window, () => whole.World.DimStrength >= 1);

        Assert.Equal(node.Key, map.Focus);
        Frame(window, "map-hover-nodes-900", "s1-");
    });
}
