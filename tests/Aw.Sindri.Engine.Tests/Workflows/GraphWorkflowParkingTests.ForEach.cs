using System.Text.Json.Nodes;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>A 'wait' node in a 'for_each' body sleeps on a timer of the batch's child workflow, once per item (#2490).</summary>
public sealed partial class GraphWorkflowParkingTests
{
    private static GraphSpec WaitInLoopSpec() => GraphRig.Spec("unlimited",
        [GraphRig.Node("start", "manual"), GraphRig.Node("each", "for_each", config: new JsonObject { ["items"] = "{{trigger.items}}" }),
            GraphRig.Node("hold", "wait", config: new JsonObject { ["duration"] = "15m" }), GraphRig.Node("gather", "collect", config: new JsonObject { ["value"] = "{{item}}" })],
        [GraphRig.Edge("start", "each"), GraphRig.Edge("each", "hold"), GraphRig.Edge("hold", "gather")]);

    private const string TwoItems = """{"kind":"manual","items":["a","b"]}""";

    private static int FifteenMinuteTimers(Temporalio.Common.WorkflowHistory history) =>
        history.Events.Count(static e => e.TimerStartedEventAttributes != null && e.TimerStartedEventAttributes.StartToFireTimeout.ToTimeSpan() == TimeSpan.FromMinutes(15));

    // why: WaitUntilRunning keys rows by node, and a loop node has one row per item
    private static async Task ItemRunning(GraphRig rig, Core.Graph.Ids.WorkflowRunId run, string node)
    {
        using var limit = Limit();
        while (!(await rig.Runs.NodeRuns(run, limit.Token)).Value.Any(n => string.Equals(n.NodeId.Value, node, StringComparison.Ordinal) && n.Status == NodeRunStatus.Running))
        {
            await Task.Delay(TimeSpan.FromMilliseconds(50), limit.Token);
        }
    }

    [Fact]
    public async Task A_wait_node_in_a_for_each_body_sleeps_once_per_item()
    {
        await using var rig = await GraphRig.StartAsync();
        var workflow = await rig.Define(WaitInLoopSpec());

        var started = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited, TwoItems);
        await SkipUntilEnded(rig, started.RunId, TimeSpan.FromMinutes(16));
        var run = await rig.Final(started.RunId);
        var batch = await rig.Client.GetWorkflowHandle($"{started.RunId.Value}/each/1").FetchHistoryAsync();
        var holds = await rig.ItemRuns(started.RunId, "hold");

        Assert.Equal(WorkflowRunStatus.Succeeded, run.Status);
        // why: a wait that ended at once also ends Succeeded; only one 15 minute timer per item in the batch's history shows each item slept
        Assert.Equal(2, FifteenMinuteTimers(batch));
        Assert.Equal([NodeRunStatus.Succeeded, NodeRunStatus.Succeeded], new[] { holds[1].Status, holds[2].Status });
    }

    [Fact]
    public async Task A_cancel_ends_a_waiting_item_cancelled_and_skips_the_rest_of_the_loop()
    {
        await using var rig = await GraphRig.StartAsync();
        var workflow = await rig.Define(WaitInLoopSpec());
        var started = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited, TwoItems);
        await ItemRunning(rig, started.RunId, "hold");

        await rig.Client.GetWorkflowHandle(started.RunId.Value).CancelAsync();
        _ = await rig.Final(started.RunId);   // why: wait for the cancel to finish, so the rig does not stop its worker with a workflow task still in flight; the rig's own time-skipping server dies with it (issue 2574)
        var holds = await rig.ItemRuns(started.RunId, "hold");

        Assert.Equal(NodeRunStatus.Cancelled, holds[1].Status);
        Assert.Equal(NodeRunStatus.Skipped, holds[2].Status);
    }
}
