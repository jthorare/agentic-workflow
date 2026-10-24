using System.Globalization;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Context.Map;
using Aw.Sindri.Ui.Views.Controls;
using Aw.Sindri.Ui.Views.Shell;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>
/// The context map's key frames (F3 spec 1, slice 2) against the in-memory API serving the map-tabs golden: every node on its
/// place, the control row, the summary card, and the 900 px window (card collapsed, no horizontal page scroll). Saved under
/// frames/ as f3-context-map-*.png when AW_UI_FRAMES=1.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class ContextMapFramesTests : FrameHost
{
    private static readonly string[] _zoomButtons = ["ZoomOutButton", "ZoomInButton", "ZoomFitButton"];

    private static string Anchor => (string)GoldenFiles.Node("context-lenses", "map-tabs")["anchor"]!["key"]!;

    // The stage has finished its fit to its present size.
    private static bool Fitted(ZoomBorder stage)
    {
        stage.Settle();
        return stage.View == ZoomView.Fit(stage.Bounds.Size, stage.WorldBounds);
    }

    private async Task<(ShellWindow Window, ShellViewModel Shell)> Open(double width, double height)
    {
        Server.WholeMapAnswer = _ => new RouteAnswer(System.Net.HttpStatusCode.InternalServerError, "{}");   // no whole map: a query draws its neighbourhood (the whole map is WholeMapFramesTests)
        var (window, shell) = await Connected(width: width, height: height);
        shell.GoTo(PageId.Context);
        shell.Context.AnchorText = Anchor;
        await shell.Context.Submit(Token);
        await Until(window, () => window.GetVisualDescendants().OfType<ContextMapWorld>().FirstOrDefault() is { NodeViews.Count: > 0 } && window.GetVisualDescendants().OfType<ZoomBorder>().FirstOrDefault() is { Bounds.Width: > 0 } stage && Fitted(stage), "the map never drew and fitted");
        Settle(window);
        return (window, shell);
    }

    [Fact]
    public Task The_map_draws_every_node_on_its_place_at_1440() => OnUi(async () =>
    {
        var (window, shell) = await Open(1440, 900);

        var map = shell.Context.Map!;
        var world = Named<ContextMapWorld>(window, "ContextWorld");
        Assert.Equal(map.Nodes.Count, world.NodeViews.Count);
        Assert.All(map.Nodes, n =>
        {
            var view = world.NodeViews[n.Key];
            Assert.Equal((n.X, n.Y, n.Width, n.Height), (Canvas.GetLeft(view), Canvas.GetTop(view), view.Width, view.Height));
            Assert.Equal(n.Name, AutomationProperties.GetName(view));
        });
        Assert.Equal(map.Pills.Count, world.PillViews.Count);
        Assert.All(map.Pills.Where(p => !p.Always), p => Assert.Equal(AccessibilityView.Raw, AutomationProperties.GetAccessibilityView(world.PillViews[p.Key].GetVisualDescendants().OfType<Border>().First())));   // an invisible label is not announced
        Assert.All(map.Pills.Where(p => p.Always), p => Assert.Equal(1, world.PillViews[p.Key].GetVisualDescendants().OfType<Border>().First().Opacity));
        Assert.Equal(map.Edges.Count, world.EdgesLayer!.Edges.Count);
        Frame(window, "context-map-1440", "f3-");
    });

    [Fact]
    public Task The_control_row_holds_the_anchor_box_the_view_switch_and_the_coverage_strip() => OnUi(async () =>
    {
        var (window, shell) = await Open(1440, 900);

        Assert.True(Visible(window, "AnchorBox"));
        Assert.Equal(Labels.ContextMapStartingPoints(1), Named<TextBlock>(window, "StartingPointsText").Text);
        var strip = Named<CoverageStrip>(window, "ContextCoverageStrip");
        Assert.Equal(shell.Context.Map!.CoverageLead, strip.Lead);
        Assert.Equal(4, strip.Areas.Count);
        Assert.False(Visible(window, "ContextAsOf"));
        Assert.False(Visible(window, "WholeMapBack"));
        Assert.True(Named<RadioButton>(window, "ViewMapButton").IsChecked);
        Assert.False(Named<RadioButton>(window, "ViewFilesButton").IsChecked);
        Assert.False(Visible(window, "ContextDraftTag"));
    });

    [Fact]
    public Task The_summary_card_is_gone_and_the_stage_keeps_its_zoom_pill() => OnUi(async () =>
    {
        var (window, _) = await Open(1440, 900);

        Assert.False(Visible(window, "ContextHud"));
        Assert.Equal(Labels.ContextMapZoomPct(Named<ZoomBorder>(window, "ContextCanvas").View.Scale), Named<TextBlock>(window, "ZoomValue").Text);
    });

    [Fact]
    public Task At_900_the_map_fits_no_smaller_than_62_percent_and_the_page_does_not_scroll_sideways() => OnUi(async () =>
    {
        var (window, _) = await Open(900, 800);

        var canvas = Named<ZoomBorder>(window, "ContextCanvas");
        var scale = canvas.View.Scale;
        Assert.Equal(ZoomView.Fit(canvas.Bounds.Size, canvas.WorldBounds).Scale, scale);
        Assert.InRange(scale, 0.62, 1.0);
        var pages = Named<ScrollViewer>(window, "Pages");
        Assert.True(pages.Extent.Width <= pages.Viewport.Width + 0.5, string.Create(CultureInfo.InvariantCulture, $"{pages.Extent.Width} > {pages.Viewport.Width}"));
        Frame(window, "context-map-900", "f3-");
    });

    [Fact]
    public Task The_view_switch_shows_the_files_in_place_of_the_map_and_back() => OnUi(async () =>
    {
        var (window, shell) = await Open(1440, 900);

        Named<RadioButton>(window, "ViewFilesButton").IsChecked = true;   // the segment pushes the choice to the view model
        Settle(window);
        Assert.True(shell.Context.ShowFiles);
        Assert.False(Visible(window, "ContextStack"));
        Assert.True(Visible(window, "ContextFilesPanel"));

        shell.Context.ChooseView(files: false);   // a choice made anywhere keeps the segments in step
        Settle(window);
        Assert.True(Named<RadioButton>(window, "ViewMapButton").IsChecked);
        Assert.False(Named<RadioButton>(window, "ViewFilesButton").IsChecked);
        shell.Context.ChooseView(files: true);
        Settle(window);
        Assert.True(Named<RadioButton>(window, "ViewFilesButton").IsChecked);
        Named<RadioButton>(window, "ViewMapButton").IsChecked = true;
        Settle(window);
        Assert.True(Visible(window, "ContextStack"));
        Assert.False(Visible(window, "ContextFilesPanel"));
    });

    [Fact]
    public Task The_zoom_pill_zooms_and_fits_and_the_stage_names_its_controls() => OnUi(async () =>
    {
        var (window, _) = await Open(1440, 900);
        var canvas = Named<ZoomBorder>(window, "ContextCanvas");
        canvas.Settle();

        var fitted = canvas.View.Scale;
        Assert.True(fitted > ZoomRange.Default.Min, "there is room to zoom out");
        canvas.ZoomOut();
        canvas.Settle();
        var smaller = canvas.View.Scale;
        canvas.Fit();
        canvas.Settle();

        Assert.True(smaller < fitted);
        Assert.Equal(fitted, canvas.View.Scale);
        Assert.Equal(Labels.ContextMapZoomPct(canvas.View.Scale), Named<TextBlock>(window, "ZoomValue").Text);
        Assert.Equal([Labels.ContextMapZoomOut, Labels.ContextMapZoomIn, Labels.ContextMapZoomFit], _zoomButtons.Select(n => AutomationProperties.GetName(Named<Button>(window, n))), StringComparer.Ordinal);
    });
}
