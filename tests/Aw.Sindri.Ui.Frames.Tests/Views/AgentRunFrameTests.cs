using System.Net;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Instance;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>An agent running a Step, drawn on the instance frame (L1): the run line, the launch's state, Attach with its served reason, and the events in words.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class AgentRunFrameTests : FrameHost
{
    [Fact]
    public Task The_frame_draws_the_run_the_launch_Attach_and_what_the_agents_steps_came_to() => OnUi(async () =>
    {
        var detail = FixtureInstances.Detail("incident-loop");
        var (window, shell) = await Connected(width: 1600, height: 1000);
        var body = GoldenFiles.Node("workflow-instance", "incident-loop");
        body["current"]!["status"]!["run"] = new JsonObject { ["runtime"] = "agent", ["launch"] = AgentRunFixtures.Launch };
        Server.WorkflowAnswer = new RouteAnswer(HttpStatusCode.OK, body.ToJsonString());
        Server.Launches[AgentRunFixtures.Launch] = RouteAnswer.Api("launches-id", "started-opened");
        Server.Items[detail.ItemId!] = new RouteAnswer(HttpStatusCode.OK, AgentRunFixtures.LiveSessionBody());
        Server.InstanceEventAnswers[detail.Id] = new RouteAnswer(HttpStatusCode.OK, AgentRunFixtures.Events(
            AgentRunFixtures.Event(6, """{"from":"review","to":"review-desk","trigger":"step.failed","reason":"lost"}""")));

        shell.OpenInstance(detail.Address, detail.Id);
        await Until(window, () => shell.Instance.Phase == FramePhase.Shown && shell.Instance.Expanded?.HasLaunchLine == true);

        Assert.Equal("An agent is working on this step", Named<TextBlock>(window, "ExpandedRunLine").Text);
        Assert.Equal(Labels.LaunchOpenedIn("Warp"), Named<TextBlock>(window, "ExpandedLaunch").Text);
        Assert.True(Visible(window, "AttachPrimary"));
        Assert.False(Visible(window, "PendingChip"));
        Assert.Equal(Labels.AttachTo(detail.Address), Heard.Name(Named<Button>(window, "AttachPrimary")));
        Assert.Contains(Named<ItemsControl>(window, "ExpandedRunEvents").GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "The agent session was lost. The run moved to review-desk.");
        Frame(window, "agent-run", "l1-frame-");
    });
}
