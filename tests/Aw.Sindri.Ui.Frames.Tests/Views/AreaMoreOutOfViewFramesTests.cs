using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.Views.Context.Whole;
using Aw.Sindri.Ui.Views.Controls;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>An opened area of 30 nodes, zoomed in: the pill over the canvas counts what is out of view, in words that fit any direction (#3674).</summary>
[Lane(TestLane.Integration)]
public sealed class AreaMoreOutOfViewFramesTests : FrameHost
{
    private const int Total = 30;

    private static RouteAnswer Area()
    {
        var golden = GoldenFiles.Node("context-whole", "area-open");
        var key = ContextAnswers.OpenArea;
        golden["nodes"] = new JsonArray([.. Enumerable.Range(0, Total).Select(i => (JsonNode)new JsonObject
        {
            ["key"] = string.Create(CultureInfo.InvariantCulture, $"code:thing{i:00}"),
            ["label"] = string.Create(CultureInfo.InvariantCulture, $"Thing{i:00}Service"),
            ["kind"] = "code",
            ["area"] = key,
            ["alsoIn"] = new JsonArray(),
            ["trust"] = "trusted",
            ["provenance"] = "deterministic",
            ["confidence"] = 1,
            ["confirmed"] = false,
            ["repo"] = "aw-split-panes",
        })]);
        golden["edges"] = new JsonArray();
        golden["area"]!["counts"] = new JsonObject { ["nodes"] = Total, ["edges"] = 0, ["byKind"] = new JsonObject { ["code"] = Total } };
        return new RouteAnswer(HttpStatusCode.OK, golden.ToJsonString());
    }

    [Fact]
    public Task Zoomed_in_on_30_nodes_the_pill_says_how_many_are_out_of_view() => OnUi(async () =>
    {
        var area = Area();
        Server.WholeMapAnswer = q => q.Area is not null ? area : ContextAnswers.WholeMap(q);
        var (window, shell) = await Connected(width: 1280, height: 900);
        shell.GoTo(PageId.Context);
        await Until(window, () => shell.Context.ShowWhole);
        Settle(window);

        await shell.Context.OpenWholeArea(ContextAnswers.OpenArea, Token);
        await Until(window, () => shell.Context.Whole.Geometry is { IsAreaCanvas: true } && window.GetVisualDescendants().OfType<WholeMapView>().Any(v => v.Canvas.Bounds.Width > 0));
        Settle(window);
        var view = Named<WholeMapView>(window, "WholeMap");
        view.Canvas.SetView(new ZoomView(2, 0, 0));
        Settle(window);
        view.Canvas.Settle();

        Assert.True(Visible(window, "WholeMoreOutOfView"));
        var text = Named<Avalonia.Controls.TextBlock>(window, "WholeMoreOutOfViewText").Text;
        Assert.EndsWith(" more out of view", text, StringComparison.Ordinal);
        Assert.DoesNotContain("below", text, StringComparison.Ordinal);
        Frame(window, "area-more-out-of-view-1280", "w6-");
    });
}
