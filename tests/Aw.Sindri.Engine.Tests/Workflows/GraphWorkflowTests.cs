using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Infrastructure.Workflows;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// 'GraphWorkflow' on the time-skipping server (issue 2258, PR 6): the walk, 'onFailure', the branch a condition takes, cancel, and the queue.
/// Each test starts its run the way a trigger does: through <see cref="GraphRunStarter"/> and <see cref="TemporalGraphRunLauncher"/>.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class GraphWorkflowTests(SindriTimeSkippingServer server) : IClassFixture<SindriTimeSkippingServer>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_linear_spec_runs_each_node_once_in_order()
    {
        await using var rig = GraphRig.Start(server.Environment);
        var workflow = await rig.Define(GraphRig.Spec("unlimited",
            [GraphRig.Node("start", "manual"), GraphRig.Node("a", "echo"), GraphRig.Node("b", "echo"), GraphRig.Node("c", "echo")],
            [GraphRig.Edge("start", "a"), GraphRig.Edge("a", "b"), GraphRig.Edge("b", "c")]));

        var started = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited);
        var run = await rig.Final(started.RunId);

        Assert.Equal(WorkflowRunStatus.Succeeded, run.Status);
        Assert.Equal(["walk-test@1:1/a", "walk-test@1:1/b", "walk-test@1:1/c"], rig.Blocks.Log);
        Assert.All(await rig.NodeStatuses(started.RunId), static s => Assert.Equal(NodeRunStatus.Succeeded, s.Value));
        Assert.Equal(4, (await rig.NodeStatuses(started.RunId)).Count);
    }

    [Fact]
    public async Task A_failed_node_halts_the_walk_and_marks_the_rest_skipped()
    {
        await using var rig = GraphRig.Start(server.Environment);
        var workflow = await rig.Define(GraphRig.Spec("unlimited",
            [GraphRig.Node("start", "manual"), GraphRig.Node("a", "echo"), GraphRig.Node("bad", "fail"), GraphRig.Node("c", "echo"), GraphRig.Node("d", "echo")],
            [GraphRig.Edge("start", "a"), GraphRig.Edge("a", "bad"), GraphRig.Edge("bad", "c"), GraphRig.Edge("c", "d")]));

        var started = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited);
        var run = await rig.Final(started.RunId);
        var nodes = await rig.NodeStatuses(started.RunId);

        Assert.Equal(WorkflowRunStatus.Failed, run.Status);
        Assert.Contains("boom", run.Error, StringComparison.Ordinal);
        Assert.Equal(["walk-test@1:1/a", "walk-test@1:1/bad"], rig.Blocks.Log);
        Assert.Equal(NodeRunStatus.Failed, nodes["bad"]);
        Assert.Equal(NodeRunStatus.Skipped, nodes["c"]);
        Assert.Equal(NodeRunStatus.Skipped, nodes["d"]);
    }

    [Fact]
    public async Task On_failure_continue_keeps_walking()
    {
        await using var rig = GraphRig.Start(server.Environment);
        var workflow = await rig.Define(GraphRig.Spec("unlimited",
            [GraphRig.Node("start", "manual"), GraphRig.Node("bad", "fail", onFailure: "continue"), GraphRig.Node("c", "echo")],
            [GraphRig.Edge("start", "bad"), GraphRig.Edge("bad", "c")]));

        var started = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited);
        var run = await rig.Final(started.RunId);
        var nodes = await rig.NodeStatuses(started.RunId);

        Assert.Equal(WorkflowRunStatus.Succeeded, run.Status);   // why: every node ran or was skipped, and no 'stop' node failed
        Assert.Equal(["walk-test@1:1/bad", "walk-test@1:1/c"], rig.Blocks.Log);
        Assert.Equal(NodeRunStatus.Failed, nodes["bad"]);
        Assert.Equal(NodeRunStatus.Succeeded, nodes["c"]);
    }

    [Fact]
    public async Task A_false_condition_takes_only_the_false_edge()
    {
        await using var rig = GraphRig.Start(server.Environment);
        var workflow = await rig.Define(GraphRig.Spec("unlimited",
            [GraphRig.Node("start", "manual"), GraphRig.Node("check", "condition", config: new JsonObject { ["when"] = "trigger.flag" }), GraphRig.Node("yes", "echo"), GraphRig.Node("no", "echo")],
            [GraphRig.Edge("start", "check"), GraphRig.Edge("check", "yes", "true"), GraphRig.Edge("check", "no", "false")]));

        var started = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited, """{"kind":"manual","flag":false}""");
        var run = await rig.Final(started.RunId);
        var nodes = await rig.NodeStatuses(started.RunId);

        Assert.Equal(WorkflowRunStatus.Succeeded, run.Status);
        Assert.Equal(["walk-test@1:1/no"], rig.Blocks.Log);
        Assert.Equal(NodeRunStatus.Skipped, nodes["yes"]);
        Assert.Equal(NodeRunStatus.Succeeded, nodes["no"]);
    }

    [Fact]
    public async Task A_cancelled_run_is_cancelled_and_mirrored()
    {
        await using var rig = GraphRig.Start(server.Environment);
        var workflow = await rig.Define(GraphRig.Spec("unlimited",
            [GraphRig.Node("start", "manual"), GraphRig.Node("wait", "hold"), GraphRig.Node("after", "echo")],
            [GraphRig.Edge("start", "wait"), GraphRig.Edge("wait", "after")]));
        var started = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited);
        await rig.Blocks.Entered.WaitAsync(TimeSpan.FromSeconds(30), Token);

        await rig.Client.GetWorkflowHandle(started.RunId.Value).CancelAsync();
        var run = await rig.Final(started.RunId);
        var nodes = await rig.NodeStatuses(started.RunId);

        Assert.Equal(WorkflowRunStatus.Cancelled, run.Status);
        Assert.Equal(NodeRunStatus.Cancelled, nodes["wait"]);
        Assert.Equal(NodeRunStatus.Skipped, nodes["after"]);
        Assert.DoesNotContain(rig.Blocks.Log, static l => string.Equals(l, "walk-test@1:1/after", StringComparison.Ordinal));
        // the ledger rows (workflow.run.cancelled, workflow.node.*) are asserted once the event types are merged (b6t)
    }

    [Fact]
    public async Task Queue_starts_the_oldest_queued_run_when_the_run_in_flight_ends()
    {
        await using var rig = GraphRig.Start(server.Environment);
        var workflow = await rig.Define(GraphRig.Spec("queue",
            [GraphRig.Node("start", "manual"), GraphRig.Node("wait", "hold")],
            [GraphRig.Edge("start", "wait")]));
        var first = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Queue);
        await rig.Blocks.Entered.WaitAsync(TimeSpan.FromSeconds(30), Token);

        var second = await rig.Trigger(workflow, occurrence: 2, GraphConcurrency.Queue);
        var queued = (await rig.Runs.Get(second.RunId, Token)).Value;
        rig.Blocks.Release();
        var firstEnded = await rig.Final(first.RunId);
        var secondEnded = await rig.Final(second.RunId);

        Assert.Equal(GraphStartKind.Queued, second.Kind);
        Assert.Equal(WorkflowRunStatus.Queued, queued.Status);
        Assert.Equal(WorkflowRunStatus.Succeeded, firstEnded.Status);
        Assert.Equal(WorkflowRunStatus.Succeeded, secondEnded.Status);
        Assert.Equal(["start walk-test@1:1/wait", "done walk-test@1:1/wait", "start walk-test@1:2/wait", "done walk-test@1:2/wait"], rig.Blocks.Log);
    }
}
