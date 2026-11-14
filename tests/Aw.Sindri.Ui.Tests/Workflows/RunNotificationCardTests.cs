using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>#3682 item 4: a notification step's card on the run page shows the title and message it sent and where it went, not an empty output.</summary>
[Lane(TestLane.Unit)]
public sealed class RunNotificationCardTests
{
    private readonly WorkflowsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<RunDetailViewModel> Opened(string status = "succeeded")
    {
        var workflow = Golden("workflows-slug", "found");
        workflow["spec"]!["nodes"]![1]!["type"] = "notify";   // the node "agent" is a notification now
        _f.Server.WorkflowAnswers[Slug] = new Aw.Sindri.Ui.Testing.RouteAnswer(System.Net.HttpStatusCode.OK, workflow.ToJsonString());
        var served = Golden("workflow-runs-run", "finished");
        var node = served["nodes"]![0]!;
        node["status"] = status;
        node["input"] = new JsonObject { ["title"] = "Drifted", ["body"] = "3 repos drifted" };
        node["output"] = new JsonObject();
        _f.ServeRun(served);
        return await _f.OpenedRun(Token);
    }

    [Fact]
    public async Task A_sent_notification_shows_its_title_its_message_and_where_it_went_and_no_empty_output()
    {
        var run = await Opened();

        var card = run.Groups[0].Runs[0];
        Assert.Equal((true, "Drifted", "3 repos drifted", "Sent to the Sindri inbox", false), (card.IsNotification, card.SentTitle, card.SentBody, card.SentTarget, card.HasOutput));
    }

    [Fact]
    public async Task A_notification_that_failed_says_nothing_was_sent()
    {
        var run = await Opened("failed");

        var card = run.Groups[0].Runs[0];
        Assert.Equal((false, string.Empty), (card.IsNotification, card.SentTarget));
    }

    [Fact]
    public async Task Other_steps_keep_their_input_and_output()
    {
        var run = await Opened();

        var start = run.Groups[1].Runs[0];
        Assert.Equal((false, true), (start.IsNotification, start.HasOutput));
    }
}
