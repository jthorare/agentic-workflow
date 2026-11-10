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
public sealed class GraphWorkflowTests
{
    private static readonly string[] _walked = ["start", "a", "b", "c"];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_linear_spec_runs_each_node_once_in_order()
    {
        await using var rig = await GraphRig.StartAsync();
        var workflow = await rig.Define(GraphRig.Spec("unlimited",
            [GraphRig.Node("start", "manual"), GraphRig.Node("a", "echo"), GraphRig.Node("b", "echo"), GraphRig.Node("c", "echo")],
            [GraphRig.Edge("start", "a"), GraphRig.Edge("a", "b"), GraphRig.Edge("b", "c")]));

        var started = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited);
        var run = await rig.Final(started.RunId);

        Assert.Equal(WorkflowRunStatus.Succeeded, run.Status);
        Assert.Equal([$"{rig.Slug}@1:1/a", $"{rig.Slug}@1:1/b", $"{rig.Slug}@1:1/c"], rig.Blocks.Log);
        Assert.All(await rig.NodeStatuses(started.RunId), static s => Assert.Equal(NodeRunStatus.Succeeded, s.Value));
        Assert.Equal(4, (await rig.NodeStatuses(started.RunId)).Count);
        Assert.Equal(
            [
                $"workflow.run.started workflow.run.started:{rig.Slug}@1:1",
                .. _walked.SelectMany(n => new[]
                {
                    $"workflow.node.started workflow.node.started:{rig.Slug}@1:1:{n}:0",
                    $"workflow.node.finished workflow.node.finished:{rig.Slug}@1:1:{n}:0",
                }),
                $"workflow.run.finished workflow.run.finished:{rig.Slug}@1:1",
            ],
            await rig.RowsWith($"workflow.run.finished workflow.run.finished:{rig.Slug}@1:1"));
    }

    [Fact]
    public async Task A_failed_node_halts_the_walk_and_marks_the_rest_skipped()
    {
        await using var rig = await GraphRig.StartAsync();
        var workflow = await rig.Define(GraphRig.Spec("unlimited",
            [GraphRig.Node("start", "manual"), GraphRig.Node("a", "echo"), GraphRig.Node("bad", "fail"), GraphRig.Node("c", "echo"), GraphRig.Node("d", "echo")],
            [GraphRig.Edge("start", "a"), GraphRig.Edge("a", "bad"), GraphRig.Edge("bad", "c"), GraphRig.Edge("c", "d")]));

        var started = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited);
        var run = await rig.Final(started.RunId);
        var nodes = await rig.NodeStatuses(started.RunId);

        Assert.Equal(WorkflowRunStatus.Failed, run.Status);
        Assert.Contains("boom", run.Error, StringComparison.Ordinal);
        Assert.Equal([$"{rig.Slug}@1:1/a", $"{rig.Slug}@1:1/bad"], rig.Blocks.Log);
        Assert.Equal(NodeRunStatus.Failed, nodes["bad"]);
        Assert.Equal(NodeRunStatus.Skipped, nodes["c"]);
        Assert.Equal(NodeRunStatus.Skipped, nodes["d"]);
    }

    [Fact]
    public async Task On_failure_continue_keeps_walking()
    {
        await using var rig = await GraphRig.StartAsync();
        var workflow = await rig.Define(GraphRig.Spec("unlimited",
            [GraphRig.Node("start", "manual"), GraphRig.Node("bad", "fail", onFailure: "continue"), GraphRig.Node("c", "echo")],
            [GraphRig.Edge("start", "bad"), GraphRig.Edge("bad", "c")]));

        var started = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited);
        var run = await rig.Final(started.RunId);
        var nodes = await rig.NodeStatuses(started.RunId);

        Assert.Equal(WorkflowRunStatus.Succeeded, run.Status);   // why: every node ran or was skipped, and no 'stop' node failed
        Assert.Equal([$"{rig.Slug}@1:1/bad", $"{rig.Slug}@1:1/c"], rig.Blocks.Log);
        Assert.Equal(NodeRunStatus.Failed, nodes["bad"]);
        Assert.Equal(NodeRunStatus.Succeeded, nodes["c"]);
    }

    [Fact]
    public async Task A_false_condition_takes_only_the_false_edge()
    {
        await using var rig = await GraphRig.StartAsync();
        var workflow = await rig.Define(GraphRig.Spec("unlimited",
            [GraphRig.Node("start", "manual"), GraphRig.Node("check", "condition", config: new JsonObject { ["when"] = "trigger.flag" }), GraphRig.Node("yes", "echo"), GraphRig.Node("no", "echo")],
            [GraphRig.Edge("start", "check"), GraphRig.Edge("check", "yes", "true"), GraphRig.Edge("check", "no", "false")]));

        var started = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited, """{"kind":"manual","flag":false}""");
        var run = await rig.Final(started.RunId);
        var nodes = await rig.NodeStatuses(started.RunId);

        Assert.Equal(WorkflowRunStatus.Succeeded, run.Status);
        Assert.Equal([$"{rig.Slug}@1:1/no"], rig.Blocks.Log);
        Assert.Equal(NodeRunStatus.Skipped, nodes["yes"]);
        Assert.Equal(NodeRunStatus.Succeeded, nodes["no"]);
    }

    [Fact]
    public async Task A_cancelled_run_is_cancelled_and_mirrored()
    {
        await using var rig = await GraphRig.StartAsync();
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
        Assert.DoesNotContain(rig.Blocks.Log, l => string.Equals(l, $"{rig.Slug}@1:1/after", StringComparison.Ordinal));
        var rows = await rig.RowsWith($"workflow.run.cancelled workflow.run.cancelled:{rig.Slug}@1:1");
        Assert.Contains($"workflow.run.cancelled workflow.run.cancelled:{rig.Slug}@1:1", rows, StringComparer.Ordinal);
        Assert.DoesNotContain(rows, static r => r.StartsWith("workflow.run.finished", StringComparison.Ordinal));
        Assert.Contains($"workflow.node.finished workflow.node.finished:{rig.Slug}@1:1:wait:0", rows, StringComparer.Ordinal);   // 'cancelled' status
        Assert.Contains($"workflow.node.finished workflow.node.finished:{rig.Slug}@1:1:after:0", rows, StringComparer.Ordinal);   // 'skipped' status
    }

    [Fact]
    public async Task Queue_starts_the_oldest_queued_run_when_the_run_in_flight_ends()
    {
        await using var rig = await GraphRig.StartAsync();
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
        Assert.Equal([$"start {rig.Slug}@1:1/wait", $"done {rig.Slug}@1:1/wait", $"start {rig.Slug}@1:2/wait", $"done {rig.Slug}@1:2/wait"], rig.Blocks.Log);
    }

    [Fact]
    public async Task A_reference_to_a_cut_output_fails_the_node_and_never_uses_the_cut_text()
    {
        await using var rig = await GraphRig.StartAsync();
        var workflow = await rig.Define(GraphRig.Spec("unlimited",
            [GraphRig.Node("start", "manual"), GraphRig.Node("big", "big"), GraphRig.Node("use", "echo", config: new JsonObject { ["text"] = "{{big.data}}" }), GraphRig.Node("after", "echo")],
            [GraphRig.Edge("start", "big"), GraphRig.Edge("big", "use"), GraphRig.Edge("use", "after")]));

        var started = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited);
        var run = await rig.Final(started.RunId);
        var nodes = (await rig.Runs.NodeRuns(started.RunId, Token)).Value.ToDictionary(static n => n.NodeId.Value, StringComparer.Ordinal);

        Assert.Equal(WorkflowRunStatus.Failed, run.Status);
        Assert.True(nodes["big"].Truncated);
        Assert.Equal(NodeRunStatus.Failed, nodes["use"].Status);
        Assert.Contains("SND-GRAPH-108", nodes["use"].Error, StringComparison.Ordinal);
        Assert.Contains("'big'", nodes["use"].Error, StringComparison.Ordinal);
        Assert.Empty(rig.Blocks.Log);   // 'use' never ran its block
        Assert.Equal(NodeRunStatus.Skipped, nodes["after"].Status);
    }
}
