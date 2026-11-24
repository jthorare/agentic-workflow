using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// 'Re-run from here' through the real walk (#3204): the restored steps are decided before the walk starts, and a restored condition takes only the branch
/// its stored row says. Before, the walk counted a branch into a step with no row as taken, so a join fed by the condition's untaken branch ran.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class RerunWalkTests
{
    [Fact]
    public async Task A_restored_condition_takes_only_its_stored_branch_so_a_join_on_its_other_branch_stays_skipped()
    {
        await using var rig = await GraphRig.StartAsync();
        var when = new JsonObject { ["when"] = "trigger.flag" };
        var workflow = await rig.Define(GraphRig.Spec("unlimited",
            [
                GraphRig.Node("start", "manual"), GraphRig.Node("c", "condition", config: when), GraphRig.Node("y", "condition", config: (JsonObject)when.DeepClone()),
                GraphRig.Node("b", "echo"), GraphRig.Node("z", "echo"), GraphRig.Node("x", "echo"),
            ],
            [
                GraphRig.Edge("start", "c"), GraphRig.Edge("start", "y"), GraphRig.Edge("c", "b", "true"), GraphRig.Edge("c", "x", "false"),
                GraphRig.Edge("y", "z", "true"), GraphRig.Edge("y", "x", "false"),
            ]));
        var first = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited, """{"kind":"manual","flag":true}""");
        Assert.Equal(WorkflowRunStatus.Succeeded, (await rig.Final(first.RunId)).Status);
        Assert.Equal(NodeRunStatus.Skipped, (await rig.NodeStatuses(first.RunId))["x"]);

        var rerun = await rig.Rerun(first.RunId, "y");
        var run = await rig.Final(rerun.Run);
        var nodes = await rig.NodeStatuses(rerun.Run);

        Assert.Equal(WorkflowRunStatus.Succeeded, run.Status);
        Assert.Equal(["start", "c", "b"], rerun.Restored);
        Assert.Equal(NodeRunStatus.Succeeded, nodes["z"]);
        Assert.Equal(NodeRunStatus.Skipped, nodes["x"]);   // why: c said true and y says true; c's 'false' edge is not taken just because x has no row in the new run
        Assert.DoesNotContain($"{rerun.Run.Value}/x", rig.Blocks.Log, StringComparer.Ordinal);
        Assert.DoesNotContain($"{rerun.Run.Value}/b", rig.Blocks.Log, StringComparer.Ordinal);   // b is restored, not run again
    }
}
