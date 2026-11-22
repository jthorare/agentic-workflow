using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// What a finished run hands to whoever reads its outputs (agent-first PR 9): an object keyed by node id, so 'run_workflow' with 'wait' has something to give. Since #3807 it holds
/// the node the spec's 'result' names, or the nodes no edge leaves, kept up to 64 KB on their own.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class GraphRunOutputsTests
{
    [Fact]
    public async Task A_run_that_succeeded_stores_the_outputs_of_its_succeeded_last_nodes_by_node_id()
    {
        await using var rig = await GraphRig.StartAsync();
        var workflow = await rig.Define(GraphRig.Spec("unlimited",
            [GraphRig.Node("start", "manual"), GraphRig.Node("a", "echo"), GraphRig.Node("boom", "fail", onFailure: "continue")],
            [GraphRig.Edge("start", "a"), GraphRig.Edge("start", "boom")]));

        var run = await rig.Final((await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited)).RunId);

        Assert.Equal(WorkflowRunStatus.Succeeded, run.Status);
        var outputs = JsonNode.Parse(run.Outputs!)!.AsObject();
        Assert.Equal("a", outputs["a"]!["node"]!.GetValue<string>());
        Assert.False(outputs.ContainsKey("boom"));
    }

    [Fact]
    public async Task Node_rows_that_pass_64_KB_together_still_give_the_last_node_output()
    {
        await using var rig = await GraphRig.StartAsync();
        var workflow = await rig.Define(GraphRig.Spec("unlimited",
            [GraphRig.Node("start", "manual"), Large("a"), Large("b"), GraphRig.Node("rank", "echo")],
            [GraphRig.Edge("start", "a"), GraphRig.Edge("a", "b"), GraphRig.Edge("b", "rank")]));

        var run = await rig.Final((await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited)).RunId);

        Assert.Equal(WorkflowRunStatus.Succeeded, run.Status);
        Assert.NotNull(run.Outputs);
        var outputs = JsonNode.Parse(run.Outputs)!.AsObject();
        Assert.Equal(["rank"], outputs.Select(static p => p.Key), StringComparer.Ordinal);
        Assert.Equal("rank", outputs["rank"]!["node"]!.GetValue<string>());
    }

    [Fact]
    public async Task A_run_whose_spec_names_a_result_gives_that_node_output()
    {
        await using var rig = await GraphRig.StartAsync();
        var spec = GraphRig.Spec("unlimited",
            [GraphRig.Node("start", "manual"), GraphRig.Node("rank", "echo"), Large("tell")],
            [GraphRig.Edge("start", "rank"), GraphRig.Edge("rank", "tell")]);
        var workflow = await rig.Define(spec with { Result = NodeId.From("rank"), });

        var run = await rig.Final((await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited)).RunId);

        var outputs = JsonNode.Parse(run.Outputs!)!.AsObject();
        Assert.Equal(["rank"], outputs.Select(static p => p.Key), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_caller_that_waits_on_a_result_cut_at_64_KB_fails_with_the_cut_code_instead_of_reading_null()
    {
        await using var rig = await GraphRig.StartAsync();
        var callee = await rig.DefineCallee(GraphRig.Spec("unlimited", [GraphRig.Node("start", "workflow_call"), GraphRig.Node("big", "big")], [GraphRig.Edge("start", "big")]));
        var workflow = await rig.Define(GraphRig.Spec("unlimited",
            [GraphRig.Node("start", "manual"), GraphRig.Node("call", "run_workflow", config: new JsonObject { ["workflow"] = callee, ["wait"] = true })],
            [GraphRig.Edge("start", "call")]));

        var run = await rig.Final((await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited)).RunId);
        var call = (await rig.Runs.NodeRuns(run.Id, TestContext.Current.CancellationToken)).Value.Single(static n => n.NodeId.Value == "call");

        Assert.Equal(WorkflowRunStatus.Failed, run.Status);
        Assert.Contains("SND-GRAPH-108", call.Error, StringComparison.Ordinal);
        Assert.Contains("'big'", call.Error, StringComparison.Ordinal);
    }

    // why: a 'pass' node outputs its config's value, so two of these pass 64 KB together while each fits
    private static GraphNode Large(string id) => GraphRig.Node(id, "pass", config: new JsonObject { ["value"] = new string('x', 40_000) });

    [Fact]
    public async Task A_run_that_failed_has_no_outputs()
    {
        await using var rig = await GraphRig.StartAsync();
        var workflow = await rig.Define(GraphRig.Spec("unlimited", [GraphRig.Node("start", "manual"), GraphRig.Node("boom", "fail")], [GraphRig.Edge("start", "boom")]));

        var run = await rig.Final((await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited)).RunId);

        Assert.Equal(WorkflowRunStatus.Failed, run.Status);
        Assert.Null(run.Outputs);
    }

    [Fact]
    public async Task A_caller_that_waits_reads_the_value_its_callee_produced()
    {
        await using var rig = await GraphRig.StartAsync();
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
