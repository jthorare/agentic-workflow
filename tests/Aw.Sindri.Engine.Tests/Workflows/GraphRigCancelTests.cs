using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// A test that cancels a graph run waits on the workflow's own result through <see cref="GraphRig.Cancel"/>, never on a 30 s polling deadline (issue 2678).
/// </summary>
[Lane(TestLane.Integration)]
public sealed class GraphRigCancelTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Cancel_waits_for_the_run_to_end_and_returns_it_cancelled()
    {
        await using var rig = await GraphRig.StartAsync();
        var workflow = await rig.Define(GraphRig.Spec("unlimited",
            [GraphRig.Node("start", "manual"), GraphRig.Node("hold", "wait", config: new JsonObject { ["duration"] = "1d" })],
            [GraphRig.Edge("start", "hold")]));
        var started = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited);
        while (!(await rig.NodeStatuses(started.RunId)).TryGetValue("hold", out var status) || status != NodeRunStatus.Running)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(50), Token);
        }

        var run = await rig.Cancel(started.RunId);

        Assert.Equal(WorkflowRunStatus.Cancelled, run.Status);
    }
}
