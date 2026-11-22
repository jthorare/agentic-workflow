using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Infrastructure.Workflows;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>A run's result is the output of one node, kept up to 64 KB on its own, never the sum of every node row (#3807).</summary>
[Lane(TestLane.Unit)]
public sealed class GraphResultOutputsTests
{
    private static readonly WorkflowRunId _run = WorkflowRunId.From("result@1:1");

    private static GraphSpec Spec(string[] nodes, (string From, string To)[] edges, string? result = null)
    {
        var spec = GraphRig.Spec("unlimited", [.. nodes.Select(static n => GraphRig.Node(n, "echo"))], [.. edges.Select(static e => GraphRig.Edge(e.From, e.To))]);
        return spec with { Result = result is null ? null : NodeId.From(result), };
    }

    private static NodeRun Row(string node, string output, int item = 0, NodeRunStatus status = NodeRunStatus.Succeeded, bool truncated = false) =>
        new(_run, NodeId.From(node), item, status, Input: null, output, truncated, Error: null, Usage: null);

    private static string Value(int length) => new JsonObject { ["value"] = new string('x', length) }.ToJsonString();

    private static JsonObject Outputs(string? text) => JsonNode.Parse(text!)!.AsObject();

    [Fact]
    public void Node_rows_that_pass_64_KB_together_still_give_the_last_node_output()
    {
        var spec = Spec(["start", "a", "b", "last"], [("start", "a"), ("a", "b"), ("b", "last")]);
        NodeRun[] rows = [Row("start", "{}"), Row("a", Value(40_000)), Row("b", Value(40_000)), Row("last", """{"findings":[1,2]}""")];

        var outputs = Outputs(GraphResultOutputs.Build(spec, rows));

        Assert.Equal(["last"], outputs.Select(static p => p.Key), StringComparer.Ordinal);
        Assert.Equal(2, outputs["last"]!["findings"]!.AsArray().Count);
    }

    [Fact]
    public void A_named_result_gives_that_node_output_only()
    {
        var spec = Spec(["start", "rank", "tell"], [("start", "rank"), ("rank", "tell")], result: "rank");
        NodeRun[] rows = [Row("start", "{}"), Row("rank", """{"top":"x"}"""), Row("tell", """{"sent":true}""")];

        var outputs = Outputs(GraphResultOutputs.Build(spec, rows));

        Assert.Equal(["rank"], outputs.Select(static p => p.Key), StringComparer.Ordinal);
        Assert.Equal("x", outputs["rank"]!["top"]!.GetValue<string>());
    }

    [Fact]
    public void Several_last_nodes_and_no_result_give_each_last_node_that_succeeded()
    {
        var spec = Spec(["start", "mid", "yes", "no"], [("start", "mid"), ("mid", "yes"), ("mid", "no")]);
        NodeRun[] rows = [Row("start", "{}"), Row("mid", """{"m":1}"""), Row("yes", """{"y":1}"""), Row("no", "{}", status: NodeRunStatus.Skipped)];

        var outputs = Outputs(GraphResultOutputs.Build(spec, rows));

        Assert.Equal(["yes"], outputs.Select(static p => p.Key), StringComparer.Ordinal);
    }

    [Fact]
    public void A_result_node_that_ran_per_item_gives_its_outputs_in_item_order()
    {
        var spec = Spec(["start", "each"], [("start", "each")]);
        NodeRun[] rows = [Row("start", "{}"), Row("each", """{"i":2}""", item: 2), Row("each", """{"i":1}""", item: 1)];

        var outputs = Outputs(GraphResultOutputs.Build(spec, rows));

        Assert.Equal([1, 2], outputs["each"]!.AsArray().Select(static o => o!["i"]!.GetValue<int>()));
    }

    [Fact]
    public void A_result_node_whose_output_was_cut_is_a_marker_with_its_size_not_null()
    {
        var spec = Spec(["start", "big"], [("start", "big")]);
        var envelope = new JsonObject { ["truncated"] = true, ["bytes"] = 70_012, ["head"] = "xxx" }.ToJsonString();
        NodeRun[] rows = [Row("start", "{}"), Row("big", envelope, truncated: true)];

        var outputs = Outputs(GraphResultOutputs.Build(spec, rows));

        var marker = outputs["big"]!.AsObject();
        Assert.True(marker["truncated"]!.GetValue<bool>());
        Assert.Equal(70_012, marker["bytes"]!.GetValue<long>());
        Assert.False(marker.ContainsKey("head"));   // why: the head would make the caller's own output pass 64 KB
        Assert.True(RunResultMarker.IsMarker(marker));
    }

    [Fact]
    public void Last_nodes_that_pass_64_KB_together_give_one_marker_with_the_size()
    {
        var spec = Spec(["start", "a", "b"], [("start", "a"), ("start", "b")]);
        NodeRun[] rows = [Row("start", "{}"), Row("a", Value(40_000)), Row("b", Value(40_000))];

        var outputs = JsonNode.Parse(GraphResultOutputs.Build(spec, rows)!);

        Assert.True(RunResultMarker.IsMarker(outputs));
        Assert.True(outputs!["bytes"]!.GetValue<long>() > NodeRunLimits.PayloadBytes);
    }

    [Fact]
    public void No_result_node_that_succeeded_gives_null()
    {
        var spec = Spec(["start", "boom"], [("start", "boom")]);
        NodeRun[] rows = [Row("start", "{}"), Row("boom", "{}", status: NodeRunStatus.Failed)];

        Assert.Null(GraphResultOutputs.Build(spec, rows));
    }
}
