using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Components;
using Aw.Sindri.Ui.ViewModels.Instance;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Instance;

/// <summary>Attach on an agent Step (L1): OpenItem:attach with the version read, the launch it started, each refusal as a sentence, and the frame reading the run behind the instance.</summary>
[Lane(TestLane.Unit)]
public sealed class AgentAttachTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeSindriApi _server = new();
    private readonly FakeClipboard _clipboard = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string ItemId => "ITM-0000000000000000000000000000000e";

    private ExpandedStateViewModel Ready()
    {
        var status = AgentRunFixtures.Status();
        var expanded = new ExpandedStateViewModel(new CurrentState(status.Header.State, Step: null, Gates: [], Desk: null, status), status.Header.Address, _clipboard, _clock)
        {
            Attacher = new AgentAttacher(_server, () => _api, ItemId),
        };
        expanded.ShowRun(new AgentRunRead(Launch: null, AgentRunFixtures.LiveSession(), [], ItemUnreadable: false));
        return expanded;
    }

    private static async Task Until(Func<bool> done)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(Token);
        limit.CancelAfter(TimeSpan.FromSeconds(10));
        while (!done())
        {
            await Task.Delay(10, limit.Token);
        }
    }

    [Fact]
    public async Task Attach_sends_OpenItem_attach_with_the_version_read_and_reads_the_launch()
    {
        _server.Launches[AgentRunFixtures.Requested] = RouteAnswer.Api("launches-id", "started-opened");
        var expanded = Ready();

        expanded.Attach!.Choose(AttachAction.Attach);
        await Until(() => expanded.AttachOutcomeLine.Length > 0);

        Assert.Equal([(ItemId, new OpenItemRequest("attach", AgentRunFixtures.LiveSession().Version))], _server.Opened);
        Assert.Equal(Labels.LaunchOpenedIn("Warp"), expanded.AttachOutcomeLine);
        Assert.False(expanded.HasAttachCommand);
    }

    [Fact]
    public async Task A_second_Attach_while_the_first_is_sending_posts_nothing()
    {
        _server.Launches[AgentRunFixtures.Requested] = RouteAnswer.Api("launches-id", "started-opened");
        _server.CommandGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var expanded = Ready();

        expanded.Attach!.Choose(AttachAction.Attach);
        expanded.Attach.Choose(AttachAction.Attach);
        await Until(() => _server.Opened.Count > 0);

        Assert.Equal((true, Labels.AttachSending), (expanded.Attach.IsPending, expanded.Attach.PendingChip));
        _server.CommandGate.SetResult();
        await Until(() => expanded.AttachOutcomeLine.Length > 0);
        _ = Assert.Single(_server.Opened);
    }

    [Fact]
    public async Task After_a_send_Attach_stays_not_ready_until_the_item_is_read_again()
    {
        _server.Launches[AgentRunFixtures.Requested] = RouteAnswer.Api("launches-id", "started-opened");
        var expanded = Ready();

        expanded.Attach!.Choose(AttachAction.Attach);
        await Until(() => expanded.AttachOutcomeLine.Length > 0);
        Assert.Equal((true, Labels.AttachSent), (expanded.Attach.IsPending, expanded.Attach.PendingChip));
        expanded.Attach.Choose(AttachAction.Attach);
        _ = Assert.Single(_server.Opened);

        expanded.ShowRun(new AgentRunRead(Launch: null, AgentRunFixtures.LiveSession(), [], ItemUnreadable: false));

        Assert.False(expanded.Attach.IsPending);
    }

    [Fact]
    public async Task A_new_send_clears_the_last_outcome_line()
    {
        _server.Launches[AgentRunFixtures.Requested] = RouteAnswer.Api("launches-id", "started-opened");
        var expanded = Ready();
        expanded.Attach!.Choose(AttachAction.Attach);
        await Until(() => expanded.AttachOutcomeLine.Length > 0);
        expanded.ShowRun(new AgentRunRead(Launch: null, AgentRunFixtures.LiveSession(), [], ItemUnreadable: false));
        _server.CommandGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        expanded.Attach.Choose(AttachAction.Attach);

        Assert.Equal(string.Empty, expanded.AttachOutcomeLine);
        _server.CommandGate.SetResult();
        await Until(() => _server.Opened.Count == 2 && expanded.AttachOutcomeLine.Length > 0);
    }

    [Fact]
    public async Task The_print_viewer_shows_the_attach_command_to_copy()
    {
        _server.Launches[AgentRunFixtures.Requested] = RouteAnswer.Api("launches-id", "started-print");
        var expanded = Ready();

        expanded.Attach!.Choose(AttachAction.Attach);
        await Until(() => expanded.HasAttachCommand);
        await expanded.CopyAttachCommand(Token);

        Assert.Equal(Labels.AttachRunThis, expanded.AttachOutcomeLine);
        Assert.Equal("tmux -L sindri-launch attach -t =snd-LCH-00000000000000000000000000000001", expanded.AttachCommand);
        Assert.Equal("tmux -L sindri-launch attach -t =snd-LCH-00000000000000000000000000000001", _clipboard.Text);
    }

    [Theory]
    [InlineData("attach-no-session-006", "SND-LAUNCH-006")]
    [InlineData("in-progress-008", "SND-LAUNCH-008")]
    [InlineData("refused-session", "SND-AUTH-002")]
    [InlineData("closed-item", "SND-ITEM-409")]
    public async Task Each_refusal_reads_as_a_sentence(string scenario, string code)
    {
        _server.Open = scenario == "refused-session" ? RouteAnswer.Refused() : RouteAnswer.Problem("items-id-open", scenario);
        var expanded = Ready();

        expanded.Attach!.Choose(AttachAction.Attach);
        await Until(() => expanded.AttachOutcomeLine.Length > 0);

        Assert.Equal(Labels.AttachRefusal(code), expanded.AttachOutcomeLine);
        Assert.False(expanded.HasAttachCommand);
    }

    [Fact]
    public async Task A_viewer_that_failed_after_the_launch_started_reads_its_code_and_detail()
    {
        _server.Launches[AgentRunFixtures.Requested] = RouteAnswer.Api("launches-id", "started-viewer-failed");
        var expanded = Ready();

        expanded.Attach!.Choose(AttachAction.Attach);
        await Until(() => expanded.AttachOutcomeLine.Length > 0);

        Assert.Equal(Labels.LaunchViewerFailed("SND-LAUNCH-001", "kitty: no display"), expanded.AttachOutcomeLine);
    }

    [Fact]
    public void A_pending_Attach_sends_nothing()
    {
        var status = AgentRunFixtures.Status();
        var expanded = new ExpandedStateViewModel(new CurrentState(status.Header.State, Step: null, Gates: [], Desk: null, status), status.Header.Address, _clipboard, _clock) { Attacher = new AgentAttacher(_server, () => _api, ItemId) };

        expanded.Attach!.Choose(AttachAction.Attach);

        Assert.Empty(_server.Opened);
    }

    [Fact]
    public async Task With_no_way_to_send_Take_over_copies_the_served_command_and_Cancel_keeps_the_served_confirm()
    {
        var expanded = Ready();
        var takeover = AgentRunFixtures.Status().Commands.First(c => c.Verb == StatusVerb.Takeover);
        var cancel = AgentRunFixtures.Status().Commands.First(c => c.Verb == StatusVerb.Cancel);

        expanded.Attach!.Choose(AttachAction.TakeOver);
        await Until(() => expanded.PendingNote.Length > 0);
        expanded.Attach.Choose(AttachAction.Cancel);

        Assert.Equal(takeover.Cli, _clipboard.Text);
        Assert.Equal(cancel.Confirm, expanded.Attach.ConfirmLine);
        Assert.True(expanded.Attach.IsConfirmingCancel);
    }

    private static RouteAnswer Run(string scenario)
    {
        var body = GoldenFiles.Node("workflow-instance", scenario);
        body["current"]!["status"]!["run"] = new JsonObject { ["runtime"] = "agent", ["launch"] = AgentRunFixtures.Launch };
        body["current"]!["status"]!["header"]!["detail"] = "agent running";
        return new RouteAnswer(HttpStatusCode.OK, body.ToJsonString());
    }

    [Fact]
    public async Task The_frame_reads_the_launch_the_item_and_the_events_behind_a_run()
    {
        var detail = FixtureInstances.Detail("incident-loop");
        _server.WorkflowAnswer = Run("incident-loop");
        _server.Launches[AgentRunFixtures.Launch] = RouteAnswer.Api("launches-id", "started-opened");
        _server.Items[detail.ItemId!] = new RouteAnswer(HttpStatusCode.OK, AgentRunFixtures.LiveSessionBody());
        _server.InstanceEventAnswers[detail.Id] = new RouteAnswer(HttpStatusCode.OK, AgentRunFixtures.Events(
            AgentRunFixtures.Event(5, """{"from":"produce","to":"review","trigger":"step.completed","via":"agent","tokens":900}"""),
            AgentRunFixtures.Event(6, """{"from":"review","to":"review-desk","trigger":"step.failed","reason":"exit"}""")));
        var frame = new InstanceFrameViewModel(_server, _clipboard, _clock);

        await frame.Open(_api, detail.Address, detail.Id, Token);

        var expanded = frame.Expanded!;
        Assert.Equal("An agent is working on this step", expanded.RunLine);
        Assert.Equal(Labels.LaunchOpenedIn("Warp"), expanded.LaunchLine);
        Assert.Equal(["via agent · 900 tokens", "The agent exited. The run moved to review-desk."], expanded.RunEvents);
    }

    [Fact]
    public async Task A_frame_opened_on_an_already_failed_instance_still_reads_the_agent_failure()
    {
        var detail = FixtureInstances.Detail("dev-desk-round-trip");   // a desk step now: the run is gone
        _server.InstanceEventAnswers[detail.Id] = new RouteAnswer(HttpStatusCode.OK, AgentRunFixtures.Events(
            AgentRunFixtures.Event(5, """{"from":"produce","to":"review","trigger":"step.completed","via":"agent","tokens":900}"""),
            AgentRunFixtures.Event(6, """{"from":"review","to":"review-desk","trigger":"step.failed","reason":"exit"}""")));
        var frame = new InstanceFrameViewModel(_server, _clipboard, _clock);

        await frame.Open(_api, detail.Address, detail.Id, Token);

        Assert.Equal((false, false), (frame.Expanded!.HasRun, frame.Expanded.HasAttach));
        Assert.Equal(["via agent · 900 tokens", "The agent exited. The run moved to review-desk."], frame.Expanded.RunEvents);
    }

    [Fact]
    public async Task Events_are_read_once_and_again_only_when_the_instance_moved()
    {
        var detail = FixtureInstances.Detail("dev-desk-round-trip");
        var frame = new InstanceFrameViewModel(_server, _clipboard, _clock);

        await frame.Open(_api, detail.Address, detail.Id, Token);
        await frame.Refresh(_api, Token);
        await frame.Refresh(_api, Token);
        Assert.Equal(1, _server.InstanceEventCalls);

        var body = GoldenFiles.Node("workflow-instance", "dev-desk-round-trip");
        body["current"]!["status"]!["header"]!["version"] = (int)body["current"]!["status"]!["header"]!["version"]! + 1;
        _server.WorkflowAnswer = new RouteAnswer(HttpStatusCode.OK, body.ToJsonString());
        await frame.Refresh(_api, Token);

        Assert.Equal(2, _server.InstanceEventCalls);
    }

    [Fact]
    public async Task A_frame_without_a_run_or_an_agent_step_reads_no_launch_and_no_item()
    {
        var detail = FixtureInstances.Detail("dev-desk-round-trip");
        var frame = new InstanceFrameViewModel(_server, _clipboard, _clock);

        await frame.Open(_api, detail.Address, detail.Id, Token);

        Assert.Equal((0, 0), (_server.LaunchCalls, _server.ItemCalls));
        Assert.Equal((false, false, false), (frame.Expanded!.HasRun, frame.Expanded.HasAttach, frame.Expanded.HasRunEvents));
    }
}
