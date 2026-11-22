using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// 'onFailure: continue' on a condition inside a for_each body (#3344): its failure takes none of the branches, so every body node after it is skipped.
/// The iteration, and so the run, must not end 'succeeded' when nothing after the failed node could run (the run's own walk got this in #3003).
/// </summary>
[Lane(TestLane.Integration)]
public sealed class OnFailureContinueInLoopTests
{
    [Fact]
    public async Task A_condition_in_a_loop_body_that_fails_and_continues_with_only_branch_edges_ends_the_run_failed()
    {
        await using var rig = await GraphRig.StartAsync();
        var workflow = await rig.Define(GraphRig.Spec("unlimited",
            [
                GraphRig.Node("start", "manual"),
                GraphRig.Node("each", "for_each", config: new JsonObject { ["items"] = "{{trigger.items}}" }),
                GraphRig.Node("check", "condition", onFailure: "continue", config: new JsonObject { ["when"] = "== ==" }),   // why: not an expression, so the block fails
                GraphRig.Node("yes", "echo"),
                GraphRig.Node("no", "echo"),
                GraphRig.Node("gather", "collect", config: new JsonObject { ["value"] = "{{item}}" }),
            ],
            [
                GraphRig.Edge("start", "each"), GraphRig.Edge("each", "check"), GraphRig.Edge("check", "yes", "true"),
                GraphRig.Edge("check", "no", "false"), GraphRig.Edge("yes", "gather"), GraphRig.Edge("no", "gather"),
            ]));

        var items = new JsonObject { ["kind"] = "manual", ["items"] = new JsonArray(1, 2) }.ToJsonString();
        var started = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited, items);
        var run = await rig.Final(started.RunId);
        var yesRuns = await rig.ItemRuns(started.RunId, "yes");

        Assert.Equal(WorkflowRunStatus.Failed, run.Status);   // why: today 'succeeded', both branches skipped on every item and nothing says why
        Assert.Contains("check", run.Error, StringComparison.Ordinal);
        Assert.DoesNotContain(yesRuns.Values, static r => r.Status == NodeRunStatus.Succeeded);
    }
}
