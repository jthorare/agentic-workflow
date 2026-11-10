using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// A rig that stops while its run still waits (a test that threw before its cancel) must not leave the run behind (issue 2574): the run's timer would later fire into a
/// task queue nobody polls, and the shared time-skipping server stops skipping time for every later test in the class. The class has its own server, so a failure here
/// can't spill into another class.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class GraphRigDisposeTests(SindriTimeSkippingServer server) : IClassFixture<SindriTimeSkippingServer>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static GraphSpec WaitSpec(string duration) => GraphRig.Spec("unlimited",
        [GraphRig.Node("start", "manual"), GraphRig.Node("hold", "wait", config: new JsonObject { ["duration"] = duration }), GraphRig.Node("after", "echo")],
        [GraphRig.Edge("start", "hold"), GraphRig.Edge("hold", "after")]);

    [Fact]
    public async Task A_run_left_waiting_by_a_stopped_rig_does_not_stop_time_for_the_next_rig()
    {
        await using (var left = GraphRig.Start(server.Environment))
        {
            var parked = await left.Trigger(await left.Define(WaitSpec("10m")), occurrence: 1, GraphConcurrency.Unlimited);
            await WaitUntilRunning(left, parked.RunId, "hold");
        }

        await using var rig = GraphRig.Start(server.Environment);
        var started = await rig.Trigger(await rig.Define(WaitSpec("15m")), occurrence: 1, GraphConcurrency.Unlimited);
        await WaitUntilRunning(rig, started.RunId, "hold");
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(Token);
        limit.CancelAfter(TimeSpan.FromSeconds(30));
        while ((await rig.Runs.Get(started.RunId, limit.Token)).Value.Status is not (WorkflowRunStatus.Succeeded or WorkflowRunStatus.Failed or WorkflowRunStatus.Cancelled))
        {
            // why: the first skip passes the left run's 10 minute timer; with that run still open, its timer fires into a queue nobody polls and this call hangs
            await server.Environment.DelayAsync(TimeSpan.FromMinutes(16), limit.Token);
            await Task.Delay(TimeSpan.FromMilliseconds(100), limit.Token);
        }

        Assert.Equal(WorkflowRunStatus.Succeeded, (await rig.Final(started.RunId)).Status);
        Assert.Equal([$"{rig.Slug}@1:1/after"], rig.Blocks.Log);
    }

    private static async Task WaitUntilRunning(GraphRig rig, WorkflowRunId run, string node)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(Token);
        limit.CancelAfter(TimeSpan.FromSeconds(30));
        while (!(await rig.NodeStatuses(run)).TryGetValue(node, out var status) || status != NodeRunStatus.Running)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(50), limit.Token);
        }
    }
}
