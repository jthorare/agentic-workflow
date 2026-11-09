using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>A body node that fails in a later batch (PR 9a review, S4): only the collect's 'onFailure' decides whether the run goes on.</summary>
[Lane(TestLane.Integration)]
public sealed class ForEachLaterBatchTests(SindriTimeSkippingServer server) : IClassFixture<SindriTimeSkippingServer>
{
    private static string Trigger() => new JsonObject
    {
        ["kind"] = "manual",
        ["items"] = new JsonArray([.. Enumerable.Range(1, 30).Select(static n => (JsonNode?)new JsonObject { ["n"] = n, ["bad"] = n == 25 })]),
    }.ToJsonString();

    private async Task<(GraphRig Rig, WorkflowRunId Id)> Start(string? collectOnFailure)
    {
        var rig = GraphRig.Start(server.Environment);
        var workflow = await rig.Define(GraphRig.Spec("unlimited",
            [GraphRig.Node("start", "manual"), GraphRig.Node("each", "for_each", config: new JsonObject { ["items"] = "{{trigger.items}}", ["batchSize"] = 20 }),
                GraphRig.Node("check", "condition", config: new JsonObject { ["when"] = "item.bad" }), GraphRig.Node("bad", "fail"),
                GraphRig.Node("gather", "collect", onFailure: collectOnFailure, config: new JsonObject { ["value"] = "{{item.n}}" }), GraphRig.Node("after", "echo")],
            [GraphRig.Edge("start", "each"), GraphRig.Edge("each", "check"), GraphRig.Edge("check", "bad", "true"), GraphRig.Edge("bad", "gather"), GraphRig.Edge("gather", "after")]));
        var started = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited, Trigger());
        return (rig, started.RunId);
    }

    [Fact]
    public async Task Item_25_failing_in_the_second_batch_fails_the_collect_and_the_run()
    {
        var (rig, id) = await Start(collectOnFailure: null);
        await using var _ = rig;

        var run = await rig.Final(id, TimeSpan.FromMinutes(2));
        var bad = await rig.ItemRuns(id, "bad");

        Assert.Equal(WorkflowRunStatus.Failed, run.Status);
        Assert.Contains("item 25", run.Error, StringComparison.Ordinal);
        Assert.Equal(NodeRunStatus.Failed, bad[25].Status);
        Assert.Equal(NodeRunStatus.Failed, (await rig.ItemRuns(id, "gather"))[0].Status);
    }

    [Fact]
    public async Task A_collect_that_continues_lets_the_run_go_on_past_the_failed_item()
    {
        var (rig, id) = await Start(collectOnFailure: "continue");
        await using var _ = rig;

        var run = await rig.Final(id, TimeSpan.FromMinutes(2));

        Assert.Equal(NodeRunStatus.Failed, (await rig.ItemRuns(id, "gather"))[0].Status);
        Assert.Equal(WorkflowRunStatus.Succeeded, run.Status);
    }
}
