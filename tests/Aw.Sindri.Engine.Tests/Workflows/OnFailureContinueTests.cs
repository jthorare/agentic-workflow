using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// 'onFailure: continue' on a node with named branches (#3003): its failure takes none of the branches, so every node after it is skipped.
/// The run must not end 'succeeded' with no error when nothing after the failed node could run.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class OnFailureContinueTests
{
    [Fact]
    public async Task A_condition_that_fails_and_continues_with_only_branch_edges_ends_the_run_failed_with_the_nodes_error()
    {
        await using var rig = await GraphRig.StartAsync();
        var workflow = await rig.Define(GraphRig.Spec("unlimited",
            [
                GraphRig.Node("start", "manual"),
                GraphRig.Node("check", "condition", onFailure: "continue", config: new JsonObject { ["when"] = "== ==" }),   // why: not an expression, so the block fails
                GraphRig.Node("yes", "echo"),
                GraphRig.Node("no", "echo"),
            ],
            [GraphRig.Edge("start", "check"), GraphRig.Edge("check", "yes", "true"), GraphRig.Edge("check", "no", "false")]));

        var started = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited);
        var run = await rig.Final(started.RunId);
        var nodes = await rig.NodeStatuses(started.RunId);

        Assert.Equal(NodeRunStatus.Failed, nodes["check"]);
        Assert.Equal(WorkflowRunStatus.Failed, run.Status);   // why: today 'succeeded' with a null error, both branches skipped and nothing says why
        Assert.Contains("check", run.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_ask_owner_that_fails_and_continues_with_only_choice_edges_ends_the_run_failed_with_the_nodes_error()
    {
        await using var rig = await GraphRig.StartAsync();
        var workflow = await rig.Define(GraphRig.Spec("unlimited",
            [
                GraphRig.Node("start", "manual"),
                GraphRig.Node("ask", "ask_owner", onFailure: "continue", config: new JsonObject { ["question"] = string.Empty }),   // why: an empty question fails the block before it asks
                GraphRig.Node("answered", "echo"),
                GraphRig.Node("timedout", "echo"),
            ],
            [GraphRig.Edge("start", "ask"), GraphRig.Edge("ask", "answered", "answered"), GraphRig.Edge("ask", "timedout", "timeout")]));

        var started = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited);
        var run = await rig.Final(started.RunId);
        var nodes = await rig.NodeStatuses(started.RunId);

        Assert.Equal(NodeRunStatus.Failed, nodes["ask"]);
        Assert.Equal(WorkflowRunStatus.Failed, run.Status);
        Assert.Contains("ask", run.Error, StringComparison.Ordinal);
    }
}
