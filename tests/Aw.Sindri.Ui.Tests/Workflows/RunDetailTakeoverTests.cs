using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Workflows;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>Take over and Hand back on a run's agent node (#2472, app half): each is sent once with the node and item, shows what Sindri said in words and reads the run again.</summary>
[Lane(TestLane.Unit)]
public sealed class RunDetailTakeoverTests
{
    private readonly WorkflowsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<(WorkflowsPageViewModel Page, NodeRunViewModel Node)> Opened(string? agent)
    {
        _f.ServeRun(agent switch { "waiting" => "agent-waiting", "taken_over" => "agent-taken-over", _ => "finished" });
        var page = await _f.Shown(Token);
        await page.OpenWorkflow(Slug, Token);
        await page.OpenRun(RunId, Token);
        return (page, page.Run.Groups.Single(g => g.Node == "agent").Runs[0]);
    }

    [Fact]
    public async Task Take_over_shows_only_on_a_waiting_agent_node()
    {
        var (_, waiting) = await Opened("waiting");
        Assert.Equal((true, false, "Take over"), (waiting.CanTakeOver, waiting.CanHandBack, waiting.TakeOverText));

        var (page, plain) = await Opened(agent: null);
        var start = page.Run.Groups.Single(g => g.Node == "start").Runs[0];
        Assert.Equal((false, false, false, false), (plain.CanTakeOver, plain.CanHandBack, start.CanTakeOver, start.CanHandBack));
    }

    [Fact]
    public async Task Hand_back_shows_only_on_a_taken_over_node()
    {
        var (_, taken) = await Opened("taken_over");

        Assert.Equal((false, true, "Hand back"), (taken.CanTakeOver, taken.CanHandBack, taken.HandBackText));
    }

    [Fact]
    public async Task Take_over_sends_the_node_and_item_once_and_rereads_the_run()
    {
        var (page, node) = await Opened("waiting");
        var reads = _f.Server.WorkflowRunCalls;

        await node.TakeOver(Token);

        Assert.Equal([$"takeover {RunId} agent 0"], _f.Server.AgentNodeControls);
        Assert.Equal(reads + 1, _f.Server.WorkflowRunCalls);
        Assert.Equal((Labels.WfTakeOverDone, false), (page.Run.NoticeText, node.IsBusy));
    }

    [Fact]
    public async Task A_second_press_while_taking_over_sends_nothing()
    {
        var (_, node) = await Opened("waiting");
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _f.Server.AgentNodeControlGate = gate;

        var first = node.TakeOver(Token);
        Assert.Equal(("Taking over…", true), (node.TakeOverText, node.IsBusy));
        await node.TakeOver(Token);
        gate.SetResult();
        await first;

        Assert.Equal([$"takeover {RunId} agent 0"], _f.Server.AgentNodeControls);
    }

    [Fact]
    public async Task A_refused_take_over_shows_the_servers_text()
    {
        var (page, node) = await Opened("waiting");
        var reads = _f.Server.WorkflowRunCalls;
        _f.Server.TakeOverAgentNodeAnswer = RouteAnswer.Problem("workflow-runs-run-nodes-node-takeover", "not-waiting");

        await node.TakeOver(Token);

        Assert.StartsWith("Couldn't take over the agent. The run 'morning-check@1:2' is not waiting on an agent", page.Run.NoticeText, StringComparison.Ordinal);
        Assert.Equal((reads, false), (_f.Server.WorkflowRunCalls, node.IsBusy));
    }

    [Fact]
    public async Task Hand_back_sends_the_node_and_item_and_rereads_the_run()
    {
        var (page, node) = await Opened("taken_over");
        var reads = _f.Server.WorkflowRunCalls;

        await node.HandBack(Token);

        Assert.Equal([$"handback {RunId} agent 0"], _f.Server.AgentNodeControls);
        Assert.Equal(reads + 1, _f.Server.WorkflowRunCalls);
        Assert.Equal((Labels.WfHandBackDone, false), (page.Run.NoticeText, node.IsBusy));
    }

    // The run's read can lag the call: the engine writes its marker late (#2472). What Sindri answered to the call wins over the re-read.
    [Fact]
    public async Task A_take_over_keeps_the_answered_state_when_the_re_read_still_says_waiting()
    {
        var (page, node) = await Opened("waiting");   // the fake keeps serving "waiting" after the call

        await node.TakeOver(Token);

        var row = page.Run.Groups.Single(g => g.Node == "agent").Runs[0];
        Assert.Equal((true, false), (row.CanHandBack, row.CanTakeOver));
    }

    [Fact]
    public async Task A_hand_back_keeps_the_answered_state_when_the_re_read_still_says_taken_over()
    {
        var (page, node) = await Opened("taken_over");   // the fake keeps serving "taken_over" after the call

        await node.HandBack(Token);

        var row = page.Run.Groups.Single(g => g.Node == "agent").Runs[0];
        Assert.Equal((true, false), (row.CanTakeOver, row.CanHandBack));
    }

    // The agent can end between the call and the re-read: a row the re-read shows with no agent stays without a button (ideas' review, #2472).
    [Fact]
    public async Task A_hand_back_shows_no_button_when_the_re_read_says_the_node_finished()
    {
        var (page, node) = await Opened("taken_over");
        _f.ServeRun("finished");

        await node.HandBack(Token);

        var row = page.Run.Groups.Single(g => g.Node == "agent").Runs[0];
        Assert.Equal((false, false), (row.CanTakeOver, row.CanHandBack));
    }
}
