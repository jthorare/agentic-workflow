using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>What a finished run hands to whoever reads its outputs (agent-first PR 9): an object keyed by node id with each succeeded node's outputs, so 'run_workflow' with 'wait' has something to give.</summary>
[Lane(TestLane.Integration)]
public sealed class GraphRunOutputsTests(SindriTimeSkippingServer server) : IClassFixture<SindriTimeSkippingServer>
{
    [Fact]
    public async Task A_run_that_succeeded_stores_the_outputs_of_its_succeeded_nodes_by_node_id()
    {
        await using var rig = GraphRig.Start(server.Environment);
        var workflow = await rig.Define(GraphRig.Spec("unlimited",
            [GraphRig.Node("start", "manual"), GraphRig.Node("a", "echo"), GraphRig.Node("boom", "fail", onFailure: "continue")],
            [GraphRig.Edge("start", "a"), GraphRig.Edge("a", "boom")]));

        var run = await rig.Final((await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited)).RunId);

        Assert.Equal(WorkflowRunStatus.Succeeded, run.Status);
        var outputs = JsonNode.Parse(run.Outputs!)!.AsObject();
        Assert.Equal("a", outputs["a"]!["node"]!.GetValue<string>());
        Assert.False(outputs.ContainsKey("boom"));
    }

    [Fact]
    public async Task A_run_that_failed_has_no_outputs()
    {
        await using var rig = GraphRig.Start(server.Environment);
        var workflow = await rig.Define(GraphRig.Spec("unlimited", [GraphRig.Node("start", "manual"), GraphRig.Node("boom", "fail")], [GraphRig.Edge("start", "boom")]));

        var run = await rig.Final((await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited)).RunId);

        Assert.Equal(WorkflowRunStatus.Failed, run.Status);
        Assert.Null(run.Outputs);
    }

    [Fact]
    public async Task A_caller_that_waits_reads_the_value_its_callee_produced()
    {
        await using var rig = GraphRig.Start(server.Environment);
        var callee = await rig.DefineCallee(GraphRig.Spec("unlimited", [GraphRig.Node("start", "workflow_call"), GraphRig.Node("a", "echo")], [GraphRig.Edge("start", "a")]));
        var workflow = await rig.Define(GraphRig.Spec("unlimited",
            [
                GraphRig.Node("start", "manual"),
                GraphRig.Node("call", "run_workflow", config: new JsonObject { ["workflow"] = callee, ["wait"] = true }),
                GraphRig.Node("after", "echo", config: new JsonObject { ["seen"] = "{{call.outputs.a.node}}" }),
            ],
            [GraphRig.Edge("start", "call"), GraphRig.Edge("call", "after")]));

        var run = await rig.Final((await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited)).RunId);

        Assert.Equal(WorkflowRunStatus.Succeeded, run.Status);
        Assert.Equal("a", (await rig.NodeInput(run.Id, "after"))!["seen"]!.GetValue<string>());
    }
}
