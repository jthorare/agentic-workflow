using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Instance;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Instance;

/// <summary>An agent running a Step, on the expanded state (L1): the run line, the launch's state in words, why Attach waits, and what the agent's Steps came to. P8's fields are optional: no run draws nothing new.</summary>
[Lane(TestLane.Unit)]
public sealed class AgentRunTests
{
    private readonly FakeClipboard _clipboard = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private ExpandedStateViewModel Expanded(StatusFields status, StepDeclaration? step = null) =>
        new(new CurrentState(status.Header.State, step, Gates: [], Desk: null, status), status.Header.Address, _clipboard, _clock);

    [Fact]
    public void A_run_reads_Agent_and_its_launch()
    {
        var expanded = Expanded(AgentRunFixtures.Status());

        Assert.Equal((true, "An agent is working on this step"), (expanded.HasRun, expanded.RunLine));
        Assert.Contains(AgentRunFixtures.Launch, expanded.RunName, StringComparison.Ordinal);
        Assert.DoesNotContain(AgentRunFixtures.Launch, expanded.RunLine, StringComparison.Ordinal);   // the launch id is for Details
        Assert.Equal("Agent session " + AgentRunFixtures.Launch, expanded.RunDetail);
        Assert.True(expanded.HasStageDetails);
        Assert.Equal((true, Labels.AttachChecking), (expanded.HasAttach, expanded.Attach!.PendingChip));
        Assert.Equal([StatusVerb.Takeover, StatusVerb.Cancel], expanded.Commands.Select(c => c.Verb));
    }

    [Fact]
    public void No_run_draws_nothing_new()
    {
        var expanded = Expanded(AgentRunFixtures.StatusWithoutRun());

        Assert.Equal((false, false, false, false, false), (expanded.HasRun, expanded.HasAttach, expanded.HasLaunchLine, expanded.HasRunEvents, expanded.HasAttachCommand));
        Assert.Null(expanded.Attach);
    }

    [Theory]
    [InlineData("requested")]
    [InlineData("started-opened")]
    [InlineData("started-unconfirmed")]
    [InlineData("started-viewer-failed")]
    [InlineData("started-print")]
    [InlineData("failed-expired-007")]
    [InlineData("failed-host-stage")]
    public void The_launchs_state_reads_as_a_sentence_from_each_golden(string scenario)
    {
        var launch = AgentRunFixtures.Golden<LaunchView>("launches-id", scenario);

        var line = RunWords.LaunchLine(launch);

        switch (scenario)
        {
            case "requested":
                Assert.Equal(Labels.LaunchRequested, line);
                break;
            case "started-opened":
                Assert.Equal(Labels.LaunchOpenedIn("Warp"), line);
                break;
            case "started-unconfirmed":
                Assert.Equal(Labels.LaunchUnconfirmed("Warp"), line);
                break;
            case "started-viewer-failed":
                Assert.Equal(Labels.LaunchViewerFailed("SND-LAUNCH-001", "kitty: no display"), line);
                break;
            case "started-print":
                Assert.Equal(Labels.LaunchRunning, line);
                Assert.Equal("tmux -L sindri-launch attach -t =snd-LCH-00000000000000000000000000000001", RunWords.Command(launch));
                break;
            default:
                Assert.Equal(Labels.LaunchFailed(launch.Code!, launch.Detail!), line);   // the code and the detail as served
                break;
        }

        Assert.Equal(scenario == "started-print", RunWords.ShowsCommand(launch));
    }

    [Fact]
    public void The_run_line_and_launch_state_come_from_what_the_frame_read()
    {
        var expanded = Expanded(AgentRunFixtures.Status());

        expanded.ShowRun(new AgentRunRead(AgentRunFixtures.Golden<LaunchView>("launches-id", "started-opened"), AgentRunFixtures.LiveSession(), [], ItemUnreadable: false));

        Assert.Equal((true, Labels.LaunchOpenedIn("Warp")), (expanded.HasLaunchLine, expanded.LaunchLine));
        expanded.ShowRun(new AgentRunRead(Launch: null, AgentRunFixtures.LiveSession(), [], ItemUnreadable: false));
        Assert.Equal(Labels.LaunchUnreadable, expanded.LaunchLine);
    }

    [Fact]
    public void Attach_is_ready_when_the_item_serves_a_live_session()
    {
        var expanded = Expanded(AgentRunFixtures.Status());

        expanded.ShowRun(new AgentRunRead(Launch: null, AgentRunFixtures.LiveSession(), [], ItemUnreadable: false));

        Assert.Equal((false, true), (expanded.Attach!.IsPending, expanded.Attach.IsPrimaryShown));
    }

    [Fact]
    public void Attach_waits_with_the_served_reason_and_never_the_P10a_words()
    {
        var running = AgentRunFixtures.Golden<ItemView>("items-id", "running");
        var starting = AgentRunFixtures.Item(l => l["inFlight"] = AgentRunFixtures.Requested);
        var blocked = AgentRunFixtures.Item(l => l["blockers"] = new System.Text.Json.Nodes.JsonArray(new System.Text.Json.Nodes.JsonObject { ["code"] = "SND-LAUNCH-002", ["detail"] = "No viewer is set up (fix: run `sindri doctor`)" }));

        Assert.Equal(Labels.AttachNoLiveSession, RunWords.PendingReason(running, unreadable: false));
        Assert.Equal(Labels.AttachLaunchStarting, RunWords.PendingReason(starting, unreadable: false));
        Assert.Equal("No viewer is set up (fix: run `sindri doctor`)", RunWords.PendingReason(blocked, unreadable: false));
        Assert.Equal(Labels.AttachItemUnreadable, RunWords.PendingReason(item: null, unreadable: true));
        Assert.Null(RunWords.PendingReason(AgentRunFixtures.LiveSession(), unreadable: false));

        var expanded = Expanded(AgentRunFixtures.Status());
        expanded.ShowRun(new AgentRunRead(Launch: null, running, [], ItemUnreadable: false));
        Assert.Equal((true, Labels.AttachNoLiveSession), (expanded.Attach!.IsPending, expanded.Attach.PendingChip));
        Assert.DoesNotContain("P10a", expanded.Attach.PendingChipName, StringComparison.Ordinal);
        Assert.EndsWith(": the agent isn't running, so there's nothing to watch", expanded.Attach.PendingChipName, StringComparison.Ordinal);
    }

    [Fact]
    public void A_completed_agent_step_reads_via_agent_with_its_tokens()
    {
        var events = new[] { new InstanceEventRow(5, "workflow.transition", A.Noon, new InstanceEventFields(From: "produce", To: "review", Trigger: "step.completed", Via: "agent", Tokens: 12345)) };

        Assert.Equal(["via agent · 12,345 tokens"], RunWords.EventLines(events));
        Assert.Equal(["via agent"], RunWords.EventLines([new InstanceEventRow(6, "workflow.transition", A.Noon, new InstanceEventFields(Trigger: "step.completed", Via: "agent"))]));
        Assert.Empty(RunWords.EventLines([new InstanceEventRow(7, "workflow.transition", A.Noon, new InstanceEventFields(Trigger: "step.completed", Via: "block", Tokens: 9))]));
        Assert.Empty(RunWords.EventLines([new InstanceEventRow(8, "workflow.started", A.Noon)]));
    }

    [Theory]
    [InlineData("launch", "The agent couldn't start")]
    [InlineData("lost", "The agent session was lost")]
    [InlineData("exit", "The agent exited")]
    [InlineData("limit", "The agent hit its limit")]
    public void A_failed_agent_step_reads_its_reason_and_where_it_went(string reason, string sentence)
    {
        var event1 = new InstanceEventRow(9, "workflow.transition", A.Noon, new InstanceEventFields(From: "produce", To: "produce-desk", Trigger: "step.failed", Reason: reason));
        var unplaced = new InstanceEventRow(10, "workflow.transition", A.Noon, new InstanceEventFields(Trigger: "step.failed", Reason: reason));

        Assert.Equal([sentence + ". The run moved to produce-desk."], RunWords.EventLines([event1]));
        Assert.Equal([sentence + "."], RunWords.EventLines([unplaced]));
    }

    [Fact]
    public void Run_events_show_on_the_state()
    {
        var expanded = Expanded(AgentRunFixtures.Status());

        expanded.ShowRun(new AgentRunRead(Launch: null, Item: null, [new InstanceEventRow(9, "workflow.transition", A.Noon, new InstanceEventFields(To: "park", Trigger: "step.failed", Reason: "lost"))], ItemUnreadable: false));

        Assert.Equal((true, 1), (expanded.HasRunEvents, expanded.RunEvents.Count));
        Assert.Equal("The agent session was lost. The run moved to park.", expanded.RunEvents[0]);
    }
}
