using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>#3683 frame wf-list-1000: at a 1000 px window the list drops its Next run column and keeps the rest, and at 1440 it keeps all six.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class WorkflowsListColumnsFramesTests : FrameHost
{
    private async Task<Window> Shown(double width)
    {
        var row = new JsonObject { ["slug"] = "sum", ["name"] = "Sum numbers", ["activeRevision"] = 1, ["latestRevision"] = 1, ["changedBy"] = "owner", ["changedAt"] = Clock.GetUtcNow().ToString("O", System.Globalization.CultureInfo.InvariantCulture), ["trigger"] = "Every 20 min", ["nextRunAt"] = Clock.GetUtcNow().AddMinutes(18).ToString("O", System.Globalization.CultureInfo.InvariantCulture) };
        Server.WorkflowsAnswer = new RouteAnswer(System.Net.HttpStatusCode.OK, new JsonObject { ["workflows"] = new JsonArray(row) }.ToJsonString());
        var (window, shell) = await Connected(width: width, height: 800);
        shell.GoTo(PageId.Workflows);
        await Until(window, () => shell.Workflows.Rows.Count == 1 && Visible(window, "WorkflowRows"), "the list never showed its row");
        Settle(window);
        return window;
    }

    private static List<string> Heads(Window window) =>
        [.. window.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Name == "ColumnHead" && t.IsEffectivelyVisible).Select(t => t.Text ?? string.Empty)];

    [Fact]
    public Task At_1000_the_Next_run_column_is_gone_and_the_others_stay() => OnUi(async () =>
    {
        var window = await Shown(1000);

        Assert.Equal(["Name", "Trigger", "Last run", "Last 10 runs", "State"], Heads(window), StringComparer.Ordinal);
        var row = window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "WorkflowRow");
        Assert.True(row.Bounds.Width <= window.Bounds.Width, "the row is wider than the window");
        Assert.DoesNotContain(row.GetVisualDescendants().OfType<TextBlock>(), t => t.IsEffectivelyVisible && t.Text == "18m");
    });

    [Fact]
    public Task At_1440_every_column_shows() => OnUi(async () =>
    {
        var window = await Shown(1440);

        Assert.Equal(["Name", "Trigger", "Last run", "Next run", "Last 10 runs", "State"], Heads(window), StringComparer.Ordinal);
    });
}
