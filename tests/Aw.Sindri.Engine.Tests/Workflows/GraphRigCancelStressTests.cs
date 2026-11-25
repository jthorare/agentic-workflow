using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// A cancel right after a wait node reads 'running' hung about 1 run in 7 (issue 2877): the node's block activity was still finishing, and the
/// time-skipping server refused the workflow's answer to the cancel and never retried it. Forty cancels in a row on one rig catch that race;
/// the heavy lane, because it takes about two minutes.
/// </summary>
[Lane(TestLane.Heavy)]
public sealed class GraphRigCancelStressTests
{
    private const int Rounds = 40;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Forty_cancels_of_a_running_wait_node_all_end_cancelled()
    {
        await using var rig = await GraphRig.StartAsync();
        var workflow = await rig.Define(GraphRig.Spec("unlimited",
            [GraphRig.Node("start", "manual"), GraphRig.Node("hold", "wait", config: new JsonObject { ["duration"] = "1d" })],
            [GraphRig.Edge("start", "hold")]));

        var ended = new List<WorkflowRunStatus>();
        for (var round = 1; round <= Rounds; round++)
        {
            var started = await rig.Trigger(workflow, occurrence: round, GraphConcurrency.Unlimited);
            while (!(await rig.NodeStatuses(started.RunId)).TryGetValue("hold", out var status) || status != NodeRunStatus.Running)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50), Token);
            }

            ended.Add((await rig.Cancel(started.RunId)).Status);
        }

        Assert.All(ended, status => Assert.Equal(WorkflowRunStatus.Cancelled, status));
    }
}
