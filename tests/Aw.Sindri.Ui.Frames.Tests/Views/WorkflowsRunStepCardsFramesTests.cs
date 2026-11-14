using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>#3682 items 3 and 4: on the run page a failed step's card has an Edit step button that opens the sheet on it, and a notification step's card draws what it sent and where.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class WorkflowsRunStepCardsFramesTests : FrameHost
{
    [Fact]
    public Task A_failed_step_has_Edit_step_that_opens_its_sheet_and_a_notification_card_shows_what_it_sent() => OnUi(async () =>
    {
        var workflow = Golden("workflows-slug", "found");
        workflow["spec"]!["nodes"]![0]!["type"] = "notify";   // "start" is the notification
        Server.WorkflowAnswers[Slug] = new RouteAnswer(System.Net.HttpStatusCode.OK, workflow.ToJsonString());
        Server.WorkflowRunsAnswers[Slug] = RouteAnswer.Api("workflows-slug-runs", "list-three");
        Server.WorkflowBlocksAnswer = RouteAnswer.Api("workflows-blocks", "listed");
        var run = Golden("workflow-runs-run", "finished");
        run["nodes"]![0]!["status"] = "failed";
        run["nodes"]![0]!["error"] = "boom";
        run["nodes"]![1]!["input"] = new JsonObject { ["title"] = "Drifted", ["body"] = "3 repos drifted" };
        run["nodes"]![1]!["output"] = new JsonObject();
        Server.WorkflowRunAnswers[RunId] = new RouteAnswer(System.Net.HttpStatusCode.OK, run.ToJsonString());
        var (window, shell) = await Connected(width: 1280, height: 1400);
        shell.GoTo(PageId.Workflows);
        await Until(window, () => shell.Workflows.Rows.Count > 0, "the Workflows page never read the list");
        await shell.Workflows.OpenWorkflow(Slug, CancellationToken.None);
        await Until(window, () => shell.Workflows.Runs.Graph is not null, "the graph never showed");
        await shell.Workflows.OpenRun(RunId, CancellationToken.None);
        await Until(window, () => Visible(window, "RunGroups") && shell.Workflows.Run.Groups.Count > 0, "the run never showed");
        Settle(window);

        Assert.True(Visible(window, "NotificationSent"), "the notification card shows nothing it sent");
        var edit = Named<Button>(window, "EditFailedStep");
        Assert.True(edit.IsEffectivelyVisible);
        Assert.Equal("Edit step", edit.Content);
        await shell.Workflows.Run.Groups.Single(g => g.Node == "agent").Runs[0].EditStep(CancellationToken.None);
        await Until(window, () => shell.Workflows.IsSettingsOpen && Visible(window, "EditorSheet"), "Edit step never opened the sheet");
    });
}
