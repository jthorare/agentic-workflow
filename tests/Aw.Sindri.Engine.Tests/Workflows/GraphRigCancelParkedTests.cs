using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// A cancel of a running 'ask_owner' node, twenty times on one rig (issue 2877): the rig sends each cancel only once the run is parked, so the
/// time-skipping server never refuses the workflow's answer. The heavy lane, because it takes about a minute.
/// </summary>
[Lane(TestLane.Heavy)]
public sealed class GraphRigCancelParkedTests
{
    private const int Rounds = 20;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Twenty_cancels_of_a_running_ask_owner_node_all_end_cancelled()
    {
        await using var rig = await GraphRig.StartAsync();
        var workflow = await rig.Define(GraphRig.Spec("unlimited",
            [GraphRig.Node("start", "manual"), GraphRig.Node("ask", "ask_owner", config: Ask()), GraphRig.Node("yes", "echo"), GraphRig.Node("no", "echo")],
            [GraphRig.Edge("start", "ask"), GraphRig.Edge("ask", "yes", "answered"), GraphRig.Edge("ask", "no", "timeout")]));

        var ended = new List<WorkflowRunStatus>();
        for (var round = 1; round <= Rounds; round++)
        {
            var started = await rig.Trigger(workflow, occurrence: round, GraphConcurrency.Unlimited);
            while (!(await rig.NodeStatuses(started.RunId)).TryGetValue("ask", out var status) || status != NodeRunStatus.Running)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50), Token);
            }

            ended.Add((await rig.Cancel(started.RunId)).Status);
        }

        Assert.All(ended, status => Assert.Equal(WorkflowRunStatus.Cancelled, status));
    }

    private static JsonObject Ask() => new()
    {
        ["question"] = "Ship the release?",
        ["choices"] = new JsonArray("yes", "no"),
        ["timeout"] = "1h",
        ["default"] = "no",
    };
}
