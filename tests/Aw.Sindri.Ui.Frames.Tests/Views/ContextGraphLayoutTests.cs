using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Views.Controls;
using Aw.Sindri.Ui.Views.Shell;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>
/// The whole-map page is the graph and nothing above it but the size line: no row of feature chips, so a repository with many features
/// still shows the graph (Context stage; whole-map spec §3.1, owner 2026-10-23). The existing frames use two features, which is why a
/// wrapping row of chips stayed green.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class ContextGraphLayoutTests : FrameHost
{
    private const double GraphFloor = 240;

    [Theory]
    [InlineData(1440, 900)]
    [InlineData(900, 800)]
    public Task A_repository_with_many_features_has_no_chip_row_and_the_graph_stays_visible(int width, int height) => OnUi(async () =>
    {
        var (window, shell) = await Open(ManyAreas(240), width, height);

        Assert.True(shell.Context.Whole.Areas.Count > 100);
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<Control>(), c => c.Name == "WholeAreas");
        var canvas = Named<ZoomBorder>(window, "WholeCanvas");
        Assert.True(canvas.Bounds.Height >= GraphFloor, string.Create(CultureInfo.InvariantCulture, $"graph {canvas.Bounds.Height}"));
        Frame(window, string.Create(CultureInfo.InvariantCulture, $"whole-map-many-features-{width}"), "w5-");
    });

    private async Task<(ShellWindow Window, ViewModels.Shell.ShellViewModel Shell)> Open(string body, int width, int height)
    {
        Server.WholeMapAnswer = _ => new RouteAnswer(HttpStatusCode.OK, body);
        var (window, shell) = await Connected(width: width, height: height);
        shell.GoTo(PageId.Context);
        await Until(window, () => shell.Context.ShowWhole && window.GetVisualDescendants().OfType<ZoomBorder>().Any(c => c.Name == "WholeCanvas" && c.Bounds.Height > 0));
        Settle(window);
        return (window, shell);
    }

    // The over-budget golden has two features. Copies of the linked one stand in for a repository this screen cannot scroll.
    private static string ManyAreas(int extra)
    {
        var node = GoldenFiles.Node("context-whole", "areas-over-budget");
        var areas = node["areas"]!.AsArray();
        var sample = areas[1]!.ToJsonString();
        for (var i = 0; i < extra; i++)
        {
            var copy = JsonNode.Parse(sample)!;
            copy["key"] = string.Create(CultureInfo.InvariantCulture, $"feature:extra.{i:000}");
            copy["label"] = string.Create(CultureInfo.InvariantCulture, $"Feature {i:000} scheduling");
            copy["unassigned"] = false;
            areas.Add(copy);
        }

        return node.ToJsonString();
    }
}
