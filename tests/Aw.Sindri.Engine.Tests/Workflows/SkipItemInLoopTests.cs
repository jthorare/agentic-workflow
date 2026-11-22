using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// 'onFailure: skip-item' on a for_each body node (#3806): the node's failure drops that one item (the rest of its body is 'skipped'), the loop goes on with the
/// next item, the collect leaves the item out and counts it in 'skipped', and the run does not fail.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class SkipItemInLoopTests
{
    private static async Task<(GraphRig Rig, WorkflowRunId Id)> Start(string body, int batchSize, params int[] items)
    {
        var rig = await GraphRig.StartAsync();
        var workflow = await rig.Define(GraphRig.Spec("unlimited",
            [GraphRig.Node("start", "manual"), GraphRig.Node("each", "for_each", config: new JsonObject { ["items"] = "{{trigger.items}}", ["batchSize"] = batchSize }),
                GraphRig.Node("a", body, onFailure: "skip-item", config: new JsonObject { ["value"] = "{{item}}" }), GraphRig.Node("b", "echo"),
                GraphRig.Node("gather", "collect", config: new JsonObject { ["value"] = "{{item}}" }), GraphRig.Node("after", "echo")],
            [GraphRig.Edge("start", "each"), GraphRig.Edge("each", "a"), GraphRig.Edge("a", "b"), GraphRig.Edge("b", "gather"), GraphRig.Edge("gather", "after")]));
        var trigger = new JsonObject { ["kind"] = "manual", ["items"] = new JsonArray([.. items.Select(static i => (JsonNode?)i)]) }.ToJsonString();
        var started = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited, trigger);
        return (rig, started.RunId);
    }

    [Theory]
    [InlineData(20)]
    [InlineData(1)]   // why: one item per batch, so the skipped item's batch and the kept items' batches are different child workflows
    public async Task A_body_node_that_fails_with_skip_item_drops_its_item_and_the_run_succeeds(int batchSize)
    {
        var (rig, id) = await Start("fail_on_two", batchSize, 1, 2, 3);
        await using var _ = rig;

        var run = await rig.Final(id, TimeSpan.FromMinutes(2));
        var a = await rig.ItemRuns(id, "a");
        var b = await rig.ItemRuns(id, "b");
        var gather = await rig.ItemRuns(id, "gather");
        var output = (await rig.NodeOutput(id, "gather"))!;

        Assert.Equal(WorkflowRunStatus.Succeeded, run.Status);
        Assert.Null(run.Error);   // why: a dropped item is not the run's failure
        Assert.Equal(NodeRunStatus.Failed, a[2].Status);
        Assert.Contains("boom", a[2].Error, StringComparison.Ordinal);
        Assert.Equal(NodeRunStatus.Skipped, b[2].Status);
        Assert.Equal(NodeRunStatus.Skipped, gather[2].Status);
        Assert.Equal(NodeRunStatus.Succeeded, b[1].Status);
        Assert.Equal(NodeRunStatus.Succeeded, b[3].Status);
        Assert.Equal(NodeRunStatus.Succeeded, gather[0].Status);
        Assert.Equal("[1,3]", output["items"]!.ToJsonString());
        Assert.Equal(2, output["count"]!.GetValue<int>());
        Assert.Equal(1, output["skipped"]!.GetValue<int>());
        Assert.Equal(NodeRunStatus.Succeeded, (await rig.NodeStatuses(id))["after"]);
    }

    [Fact]
    public async Task Every_item_skipped_still_completes_the_loop_with_an_empty_list()
    {
        var (rig, id) = await Start("fail", batchSize: 20, 1, 2);
        await using var _ = rig;

        var run = await rig.Final(id, TimeSpan.FromMinutes(2));
        var output = (await rig.NodeOutput(id, "gather"))!;

        Assert.Equal(WorkflowRunStatus.Succeeded, run.Status);
        Assert.Null(run.Error);
        Assert.Equal("[]", output["items"]!.ToJsonString());
        Assert.Equal(0, output["count"]!.GetValue<int>());
        Assert.Equal(2, output["skipped"]!.GetValue<int>());
        Assert.Equal(NodeRunStatus.Succeeded, (await rig.NodeStatuses(id))["after"]);
    }

    [Fact]
    public async Task A_loop_with_nothing_skipped_says_skipped_0()
    {
        var (rig, id) = await Start("fail_on_two", batchSize: 20, 1, 3);
        await using var _ = rig;

        var run = await rig.Final(id, TimeSpan.FromMinutes(2));
        var output = (await rig.NodeOutput(id, "gather"))!;

        Assert.Equal(WorkflowRunStatus.Succeeded, run.Status);
        Assert.Equal(0, output["skipped"]!.GetValue<int>());
    }
}
