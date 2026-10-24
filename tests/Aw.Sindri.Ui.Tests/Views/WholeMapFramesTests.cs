using System.Globalization;
using System.Net;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Context.Whole;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Context.Whole;
using Aw.Sindri.Ui.Views.Controls;
using Aw.Sindri.Ui.Views.Shell;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>
/// The Context page opening on the whole map (whole-map spec §3.1) against the in-memory API serving context's whole-map goldens
/// (Golden/Api/context-whole, from #830): the map drawn in one canvas, the list of features, the first open's zoom, no sideways scroll at 900 px, the
/// by-feature overview, and the No map state over it. Saved under frames/ as whole-map-*.png when AW_UI_FRAMES=1.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class WholeMapFramesTests : FrameHost
{
    private static RouteAnswer Draft(string name) => new(HttpStatusCode.OK, GoldenFiles.Api("context-whole", name));

    private async Task<(ShellWindow Window, ShellViewModel Shell)> Open(double width, double height, RouteAnswer? answer = null)
    {
        if (answer is { } served)
        {
            Server.WholeMapAnswer = q => q.Area is null ? served : ContextAnswers.WholeMap(q);   // an area is read in full from the goldens
        }

        var (window, shell) = await Connected(width: width, height: height);
        shell.GoTo(PageId.Context);
        await Until(window, () => shell.Context.ShowWhole && window.GetVisualDescendants().OfType<WholeMapView>().Any(v => v.Canvas.Bounds.Width > 0));
        Settle(window);
        return (window, shell);
    }

    [Fact]
    public Task The_page_opens_on_the_whole_map_with_every_feature_listed_and_the_first_zoom_in_range() => OnUi(async () =>
    {
        var (window, shell) = await Open(1440, 900);

        var whole = Named<WholeMapView>(window, "WholeMap");
        whole.Canvas.Settle();
        Assert.Equal(shell.Context.Whole.Geometry!.World.Width, whole.Canvas.WorldBounds.Width);
        Assert.InRange(whole.Canvas.View.Scale, WholeMapViewModel.FirstOpenFloor, WholeMapViewModel.MaxScale);
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<Control>(), c => c.Name == "WholeAreas");   // the page is the graph: no row of feature chips
        Assert.Equal(shell.Context.Whole.Head, shell.Context.Head);
        Assert.False(Visible(window, "WholeStatus"));
        Assert.False(Visible(window, "ContextEmpty"));
        Frame(window, "whole-map-1440", "w3-");
    });

    [Fact]
    public Task At_900_the_page_does_not_scroll_sideways() => OnUi(async () =>
    {
        var (window, _) = await Open(900, 800);

        var pages = Named<ScrollViewer>(window, "Pages");
        Assert.True(pages.Extent.Width <= pages.Viewport.Width + 0.5, string.Create(CultureInfo.InvariantCulture, $"{pages.Extent.Width} > {pages.Viewport.Width}"));
        Frame(window, "whole-map-900", "w3-");
    });

    [Fact]
    public Task A_map_over_the_budget_opens_by_feature_without_a_grouping_line_and_a_click_on_a_feature_opens_it() => OnUi(async () =>
    {
        var (window, shell) = await Open(1440, 900, Draft("areas-over-budget"));

        Assert.True(shell.Context.Whole.ByArea);
        Assert.False(Visible(window, "WholeNote"));
        Frame(window, "whole-map-by-area-1440", "w3-");

        shell.Context.Whole.Areas.First(a => a.Key == ContextAnswers.OpenArea).Open();
        await Until(window, () => shell.Context.Whole.IsAreaOpen);
        Settle(window);

        Assert.Equal([("~", true), ("Tab groups", false)], shell.Context.Whole.Crumbs.Select(c => (c.Text, c.IsLink)));
        Assert.True(Visible(window, "WholeCrumbs"));
        Assert.Equal(6, shell.Context.Whole.Geometry!.Nodes.Count);
    });

    [Fact]
    public Task No_map_shows_the_existing_empty_state_not_the_whole_map() => OnUi(async () =>
    {
        Server.WholeMapAnswer = _ => RouteAnswer.Problem(HttpStatusCode.NotFound, "SND-CONTEXT-030", "no map yet (fix: sindri context map build)");
        var (window, shell) = await Connected(width: 1440, height: 900);

        shell.GoTo(PageId.Context);
        await Until(window, () => shell.Context.ShowEmpty);
        Settle(window);

        Assert.True(Visible(window, "ContextEmpty"));
        Assert.False(Visible(window, "WholeMapHost"));
        Assert.False(Visible(window, "WholeStatus"));
    });

    [Fact]
    public Task A_failed_read_shows_the_failure_row_with_retry_that_reads_again() => OnUi(async () =>
    {
        var failing = true;
        Server.WholeMapAnswer = _ => failing ? new RouteAnswer(HttpStatusCode.InternalServerError, "{}") : Draft("nodes-small");
        var (window, shell) = await Connected(width: 1440, height: 900);

        shell.GoTo(PageId.Context);
        await Until(window, () => shell.Context.Whole.IsFailed);
        Settle(window);
        Assert.True(Visible(window, "WholeStatus"));
        Assert.True(Visible(window, "WholeRetry"));

        failing = false;
        Named<Button>(window, "WholeRetry").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        await Until(window, () => shell.Context.ShowWhole);

        Assert.False(Visible(window, "WholeStatus"));
    });

    // The mini-map floats over the stage's bottom-left corner: no card may be drawn under it on the first open or after Fit, at the narrow width where the map fills the stage.
    [Theory]
    [InlineData("nodes-small")]
    [InlineData("areas-over-budget")]
    public Task The_mini_map_covers_no_card_on_the_first_open_or_after_fit_at_900(string golden) => OnUi(async () =>
    {
        var (window, shell) = await Open(900, 800, Draft(golden));
        var whole = Named<WholeMapView>(window, "WholeMap");
        whole.Canvas.Settle();
        var miniMap = Named<Border>(window, "WholeMiniMapHost").Bounds;

        Assert.Empty(CardsOver(shell.Context.Whole.Geometry!, whole.Canvas.View, miniMap));

        Named<Button>(window, "WholeZoomFit").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        whole.Canvas.Settle();

        Assert.Empty(CardsOver(shell.Context.Whole.Geometry!, whole.Canvas.View, miniMap));
    });

    // The cards (tiles by feature, columns by node) whose rectangle on the stage meets the mini-map's.
    private static List<Avalonia.Rect> CardsOver(WholeMapGeometry geometry, ZoomView view, Avalonia.Rect miniMap)
    {
        var rects = geometry.ByArea ? geometry.Tiles.Select(t => t.Rect) : geometry.Columns.Select(c => c.Rect);
        return [.. rects
            .Select(r => new Avalonia.Rect((r.X * view.Scale) + view.X, (r.Y * view.Scale) + view.Y, r.Width * view.Scale, r.Height * view.Scale))
            .Where(r => r.Intersects(miniMap))];
    }

    // The proof of whole-map spec §8 for W3: a frame of each state at both widths (saved when AW_UI_FRAMES=1) and the first-frame time on an over-budget map.
    [Theory]
    [InlineData(1440, 900, "nodes-small", false)]
    [InlineData(900, 800, "nodes-small", false)]
    [InlineData(1440, 900, "areas-over-budget", false)]
    [InlineData(900, 800, "areas-over-budget", false)]
    [InlineData(1440, 900, "areas-over-budget", true)]
    [InlineData(900, 800, "areas-over-budget", true)]
    public Task Every_map_state_draws_at_both_widths(double width, double height, string golden, bool openArea) => OnUi(async () =>
    {
        var (window, shell) = await Open(width, height, Draft(golden));
        if (openArea)
        {
            shell.Context.Whole.Areas.First(a => a.Key == ContextAnswers.OpenArea).Open();
            await Until(window, () => shell.Context.Whole.IsAreaOpen);
        }

        Frame(window, string.Create(CultureInfo.InvariantCulture, $"{(openArea ? "area-open" : golden)}-{(int)width}"), "w3-");

        Assert.Equal(openArea, shell.Context.Whole.IsAreaOpen);
    });

    [Fact]
    public Task The_loading_failed_and_no_map_states_draw_at_1440() => OnUi(async () =>
    {
        var gate = new TaskCompletionSource();
        Server.WholeMapGate = gate;
        var (window, shell) = await Connected(width: 1440, height: 900);
        shell.GoTo(PageId.Context);
        await Until(window, () => shell.Context.Whole.IsLoading);
        Frame(window, "loading-1440", "w3-");
        Server.WholeMapGate = null;
        Server.WholeMapAnswer = _ => new RouteAnswer(HttpStatusCode.InternalServerError, "{}");
        _ = gate.TrySetResult();
        await Until(window, () => shell.Context.Whole.IsFailed);
        Frame(window, "failed-1440", "w3-");
        Server.WholeMapAnswer = _ => RouteAnswer.Problem(HttpStatusCode.NotFound, "SND-CONTEXT-030", "no map yet (fix: sindri context map build)");
        _ = shell.Context.ReadWhole(Token);
        await Until(window, () => shell.Context.ShowEmpty);
        Frame(window, "no-map-1440", "w3-");

        Assert.True(Visible(window, "ContextEmpty"));
    });

    [Fact]
    public Task The_first_frame_of_an_over_budget_map_comes_within_a_second() => OnUi(async () =>
    {
        Server.WholeMapAnswer = _ => Draft("areas-over-budget");
        var (window, shell) = await Connected(width: 1440, height: 900);
        var clock = System.Diagnostics.Stopwatch.StartNew();

        shell.GoTo(PageId.Context);
        await Until(window, () => shell.Context.ShowWhole && window.GetVisualDescendants().OfType<WholeMapView>().Any(v => v.Canvas.Bounds.Width > 0));
        Settle(window);
        clock.Stop();

        var dir = Path.Combine(AppContext.BaseDirectory, "frames");
        _ = Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(Path.Combine(dir, "w3-first-frame-ms.txt"), clock.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture), Token);
        Assert.True(clock.ElapsedMilliseconds < 1000, string.Create(CultureInfo.InvariantCulture, $"first frame took {clock.ElapsedMilliseconds} ms"));
    });

    // The proof of whole-map spec §8 for W4: the camera on a query's answer with the rest dimmed behind it, then back on the whole map after Esc in the starting-point box.
    [Fact]
    public Task A_query_zooms_into_the_map_and_esc_in_the_box_goes_back_to_the_whole_map_at_1440() => OnUi(async () =>
    {
        var (window, shell) = await Open(1440, 900);
        shell.Context.AnchorText = (string)GoldenFiles.Node("context-lenses", "map-tabs")["anchor"]!["key"]!;
        var whole = Named<WholeMapView>(window, "WholeMap");
        var fitted = whole.Canvas.View;

        await shell.Context.Submit(Token);
        await Until(window, () => shell.Context.IsZoomedIn);
        whole.Canvas.Settle();
        Frame(window, "zoomed-1440", "w4-");

        Assert.Equal((true, true), (shell.Context.ShowWhole, shell.Context.Whole.HitKeys.Count > 0));
        Assert.True(whole.Canvas.View.Scale > fitted.Scale || whole.Canvas.View != fitted);
        var box = Named<TextBox>(window, "AnchorBox");
        box.RaiseEvent(new Avalonia.Input.KeyEventArgs { RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Escape });
        Settle(window);
        whole.Canvas.Settle();
        Frame(window, "back-1440", "w4-");

        Assert.Equal((false, false), (shell.Context.IsZoomedIn, shell.Context.Whole.IsZoomed));
        Assert.Equal(shell.Context.Whole.Head, shell.Context.Head);
    });
}
