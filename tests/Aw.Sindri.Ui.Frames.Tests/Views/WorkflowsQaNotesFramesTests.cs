using System.Text.Json.Nodes;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>#3683 qa notes: the pause glyph has the automation name "Paused" (the State column can drop, the glyph stays), and one or three ticked runs show a hint instead of Compare.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class WorkflowsQaNotesFramesTests : FrameHost
{
    [Fact]
    public Task The_pause_glyph_of_a_paused_workflow_is_named_Paused() => OnUi(async () =>
    {
        Server.WorkflowsAnswer = new RouteAnswer(System.Net.HttpStatusCode.OK, new JsonObject
        {
            ["workflows"] = new JsonArray(new JsonObject { ["slug"] = "gate", ["name"] = "Nightly main gate", ["activeRevision"] = 1, ["latestRevision"] = 1, ["changedBy"] = "owner", ["changedAt"] = Clock.GetUtcNow().ToString("O", System.Globalization.CultureInfo.InvariantCulture), ["paused"] = true }),
        }.ToJsonString());
        var (window, shell) = await Connected(width: 1440, height: 800);
        shell.GoTo(PageId.Workflows);
        await Until(window, () => shell.Workflows.Rows.Count == 1 && Visible(window, "WorkflowRows"), "the list never showed");
        Settle(window);

        var glyph = window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "RowPausedGlyph");
        Assert.Equal("Paused", AutomationProperties.GetName(glyph));
    });

    [Fact]
    public Task One_ticked_run_shows_the_hint_and_two_show_Compare() => OnUi(async () =>
    {
        Server.WorkflowRunsAnswers[Slug] = RouteAnswer.Api("workflows-slug-runs", "list-three");
        var (window, shell) = await Connected(width: 1440, height: 800);
        shell.GoTo(PageId.Workflows);
        await Until(window, () => shell.Workflows.Rows.Count > 0, "the list never showed");
        await shell.Workflows.OpenWorkflow(Slug, CancellationToken.None);
        await Until(window, () => shell.Workflows.Runs.Rows.Count == 3 && Visible(window, "RunRows"), "the runs never showed");

        shell.Workflows.Runs.Rows[0].IsSelected = true;
        await Until(window, () => Visible(window, "RunsCompareHint"), "the hint never showed");
        Assert.Equal("Tick one more run to compare", Named<TextBlock>(window, "RunsCompareHint").Text);
        Assert.False(Visible(window, "RunsCompare"));

        shell.Workflows.Runs.Rows[1].IsSelected = true;
        await Until(window, () => Visible(window, "RunsCompare") && !Visible(window, "RunsCompareHint"), "Compare never replaced the hint");
    });
}
