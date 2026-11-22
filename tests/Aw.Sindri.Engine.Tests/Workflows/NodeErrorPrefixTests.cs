using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>A node run's error is the bare "code: message"; a run's error names the failed node once, never nested (#3304).</summary>
[Lane(TestLane.Integration)]
public sealed class NodeErrorPrefixTests
{
    private static int Prefixes(string? text) => text is null ? 0 : text.Split("node '").Length - 1;

    [Fact]
    public async Task A_failing_node_run_has_the_bare_error_and_the_run_names_the_node_once()
    {
        await using var rig = await GraphRig.StartAsync();
        var workflow = await rig.Define(GraphRig.Spec("unlimited",
            [GraphRig.Node("start", "manual"), GraphRig.Node("bad", "fail")],
            [GraphRig.Edge("start", "bad")]));

        var started = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited);
        var run = await rig.Final(started.RunId);
        var bad = (await rig.ItemRuns(started.RunId, "bad"))[0];

        Assert.Equal(0, Prefixes(bad.Error));
        Assert.StartsWith("node 'bad' failed: ", run.Error, StringComparison.Ordinal);
        Assert.Equal(1, Prefixes(run.Error));
    }

    [Fact]
    public async Task A_for_each_body_failure_gives_a_run_error_with_the_node_prefix_once_and_bare_node_rows()
    {
        await using var rig = await GraphRig.StartAsync();
        var workflow = await rig.Define(GraphRig.Spec("unlimited",
            [GraphRig.Node("start", "manual"), GraphRig.Node("each", "for_each", config: new JsonObject { ["items"] = "{{trigger.items}}" }), GraphRig.Node("bad", "fail"), GraphRig.Node("gather", "collect", config: new JsonObject { ["value"] = "{{item}}" })],
            [GraphRig.Edge("start", "each"), GraphRig.Edge("each", "bad"), GraphRig.Edge("bad", "gather")]));

        var started = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited, new JsonObject { ["kind"] = "manual", ["items"] = new JsonArray(1, 2) }.ToJsonString());
        var run = await rig.Final(started.RunId);

        Assert.Equal(1, Prefixes(run.Error));
        Assert.Contains("node 'bad' failed on item 1: ", run.Error, StringComparison.Ordinal);
        Assert.Equal(0, Prefixes((await rig.ItemRuns(started.RunId, "gather"))[0].Error));
        Assert.Equal(0, Prefixes((await rig.ItemRuns(started.RunId, "bad"))[1].Error));
    }

    [Fact]
    public async Task A_for_each_body_node_that_continues_with_no_out_edge_keeps_the_prefix_once_and_a_bare_collect_row()
    {
        await using var rig = await GraphRig.StartAsync();
        var workflow = await rig.Define(GraphRig.Spec("unlimited",
            [GraphRig.Node("start", "manual"), GraphRig.Node("each", "for_each", config: new JsonObject { ["items"] = "{{trigger.items}}" }), GraphRig.Node("a", "fail", onFailure: "continue"),
                GraphRig.Node("c", "condition", onFailure: "continue", config: new JsonObject { ["when"] = "a.x" }), GraphRig.Node("gather", "collect", config: new JsonObject { ["value"] = "{{item}}" })],
            [GraphRig.Edge("start", "each"), GraphRig.Edge("each", "a"), GraphRig.Edge("a", "c"), GraphRig.Edge("c", "gather", "true")]));

        var started = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited, new JsonObject { ["kind"] = "manual", ["items"] = new JsonArray(1, 2) }.ToJsonString());
        var run = await rig.Final(started.RunId);

        Assert.Equal(1, Prefixes(run.Error));
        Assert.Contains("has no 'out' edge to continue on", run.Error, StringComparison.Ordinal);
        Assert.Equal(0, Prefixes((await rig.ItemRuns(started.RunId, "gather"))[0].Error));
    }
}
