using System.Globalization;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>#3683 (review of #4395): a long workflow name ends in an ellipsis before the Trigger column, and a list row holds its content at text scale 1.3 (frame wf-list-1000).</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class WorkflowsListFitFramesTests : FrameHost
{
    private const string LongName = "Nightly drift check of every repo we keep for the team, with the release notes, the open pull requests and the flaky test report";   // wider than the Name column's 360 px maximum, so it trims at any width

    private async Task<Window> Shown(double width, string name)
    {
        var row = new JsonObject { ["slug"] = "sum", ["name"] = name, ["activeRevision"] = 1, ["latestRevision"] = 1, ["changedBy"] = "owner", ["changedAt"] = Clock.GetUtcNow().ToString("O", CultureInfo.InvariantCulture), ["trigger"] = "Every 20 min" };
        Server.WorkflowsAnswer = new RouteAnswer(System.Net.HttpStatusCode.OK, new JsonObject { ["workflows"] = new JsonArray(row) }.ToJsonString());
        var (window, shell) = await Connected(width: width, height: 800);
        shell.GoTo(PageId.Workflows);
        await Until(window, () => shell.Workflows.Rows.Count == 1 && Visible(window, "WorkflowRows"), "the list never showed its row");
        Settle(window);
        return window;
    }

    [Fact]
    public Task A_long_name_at_1000_ends_before_the_Trigger_column() => OnUi(async () =>
    {
        var window = await Shown(1000, LongName);

        var row = window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "WorkflowRow");
        var title = row.GetVisualDescendants().OfType<TextBlock>().Single(t => string.Equals(t.Text, LongName, StringComparison.Ordinal));
        var cell = Assert.IsType<Grid>(title.FindAncestorOfType<Grid>());   // the Name cell: its width is the Name column
        var trigger = row.GetVisualDescendants().OfType<TextBlock>().Single(t => string.Equals(t.Text, "Every 20 min", StringComparison.Ordinal));
        var titleRight = title.TranslatePoint(new Point(title.Bounds.Width, 0), window)!.Value.X;
        var triggerLeft = trigger.TranslatePoint(default, window)!.Value.X;

        Assert.True(titleRight <= triggerLeft, string.Create(CultureInfo.InvariantCulture, $"the title ends at x={titleRight}, past the Trigger column at x={triggerLeft}"));
        Assert.True(title.Bounds.Width <= cell.Bounds.Width + 0.5 && cell.Bounds.Width <= 360.5, string.Create(CultureInfo.InvariantCulture, $"the title is {title.Bounds.Width} px in a {cell.Bounds.Width} px cell: it should trim inside it"));
    });

    [Fact]
    public Task At_text_scale_1_3_a_list_row_holds_its_content() => OnUi(async () =>
    {
        var window = await Shown(1000, "Sum numbers");

        await WorkflowsFitAsserts.AtScale(1.3, () =>
        {
            Settle(window);
            WorkflowsFitAsserts.AssertNothingClips(window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "WorkflowRow"), "the list row");
            return Task.CompletedTask;
        });
    });

    private async Task<Window> ShownGroups(double width)
    {
        var now = Clock.GetUtcNow().ToString("O", CultureInfo.InvariantCulture);
        JsonObject Row(string slug, string name, JsonObject? last) => new() { ["slug"] = slug, ["name"] = name, ["activeRevision"] = 1, ["latestRevision"] = 1, ["changedBy"] = "owner", ["changedAt"] = now, ["trigger"] = "By hand", ["lastRun"] = last };
        var failed = new JsonObject { ["run"] = "flaky@1:1", ["status"] = "failed", ["at"] = now };
        Server.WorkflowsAnswer = new RouteAnswer(System.Net.HttpStatusCode.OK, new JsonObject { ["workflows"] = new JsonArray(Row("flaky", "Flaky check", failed), Row("sum", "Sum numbers", last: null)) }.ToJsonString());
        var (window, shell) = await Connected(width: width, height: 800);
        shell.GoTo(PageId.Workflows);
        await Until(window, () => shell.Workflows.Rows.Count == 2 && Visible(window, "WorkflowRows"), "the list never showed its rows");
        Settle(window);
        return window;
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.3)]
    public Task A_group_header_is_one_line_with_its_hint_on_it_and_no_band_under_it(double scale) => OnUi(async () =>
    {
        var window = await ShownGroups(1000);

        await WorkflowsFitAsserts.AtScale(scale, () =>
        {
            Settle(window);
            var labels = window.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Name == "RowGroupHeader" && t.IsEffectivelyVisible).ToList();
            Assert.Equal(2, labels.Count);   // "Needs a look · 1" and "Everything else · 1"
            foreach (var label in labels)
            {
                var header = Assert.IsType<Grid>(label.Parent);
                var hint = Assert.IsType<TextBlock>(header.Children[1]);
                var dy = Math.Abs(hint.TranslatePoint(default, header)!.Value.Y - label.TranslatePoint(default, header)!.Value.Y);
                Assert.True(dy <= 4, string.Create(CultureInfo.InvariantCulture, $"'{label.Text}': the hint is {dy} px below its label: it should share the line"));
                var band = header.Bounds.Height - label.Bounds.Height;
                Assert.True(band <= 4, string.Create(CultureInfo.InvariantCulture, $"'{label.Text}': the header is {header.Bounds.Height} px high for a {label.Bounds.Height} px label: an empty band of {band} px"));
            }

            Assert.Equal(1, window.GetVisualDescendants().OfType<Border>().Count(b => b.Name == "GroupRule" && b.IsEffectivelyVisible));   // the rule above "Everything else", as every group after the first has
            return Task.CompletedTask;
        });
    });
}
