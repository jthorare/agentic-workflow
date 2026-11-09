using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>'for_each' and 'collect' on the time-skipping server (PR 9): items run in batches as child workflows, the parent's history stays small, and the values come back in item order.</summary>
[Lane(TestLane.Integration)]
public sealed class ForEachWorkflowTests(SindriTimeSkippingServer server) : IClassFixture<SindriTimeSkippingServer>
{
    private static readonly TimeSpan _long = TimeSpan.FromMinutes(4);

    private static string Trigger(IEnumerable<JsonNode?> items) => new JsonObject { ["kind"] = "manual", ["items"] = new JsonArray([.. items]) }.ToJsonString();

    private static IEnumerable<JsonNode?> Numbers(int count) => Enumerable.Range(1, count).Select(static i => (JsonNode?)JsonValue.Create(i));

    private static JsonObject Each(int? maxItems = null) =>
        maxItems is { } cap ? new JsonObject { ["items"] = "{{trigger.items}}", ["maxItems"] = cap } : new JsonObject { ["items"] = "{{trigger.items}}" };

    [Fact]
    public async Task Two_hundred_items_run_in_batches_and_collect_in_item_order()
    {
        await using var rig = GraphRig.Start(server.Environment);
        var workflow = await rig.Define(GraphRig.Spec("unlimited",
            [GraphRig.Node("start", "manual"), GraphRig.Node("each", "for_each", config: Each()), GraphRig.Node("tag", "pass", config: new JsonObject { ["value"] = "{{item}}" }),
                GraphRig.Node("gather", "collect", config: new JsonObject { ["value"] = "{{tag.value}}" })],
            [GraphRig.Edge("start", "each"), GraphRig.Edge("each", "tag"), GraphRig.Edge("tag", "gather")]));

        var started = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited, Trigger(Numbers(200)));
        var run = await rig.Final(started.RunId, _long);
        var gathered = (await rig.ItemRuns(started.RunId, "gather"))[0];
        var output = JsonNode.Parse(gathered.Output!)!;

        Assert.Equal(WorkflowRunStatus.Succeeded, run.Status);
        Assert.Equal(NodeRunStatus.Succeeded, gathered.Status);
        Assert.Equal(200, output["count"]!.GetValue<int>());
        Assert.Equal(Enumerable.Range(1, 200), output["items"]!.AsArray().Select(static n => n!.GetValue<int>()));
        var tagRuns = await rig.ItemRuns(started.RunId, "tag");
        Assert.Equal(200, tagRuns.Count);
        Assert.Equal(7, JsonNode.Parse(tagRuns[7].Output!)!["value"]!.GetValue<int>());   // item index 7 is the 7th item: indexes count from 1
    }

    [Fact]
    public async Task Five_hundred_items_stay_under_the_history_budget()
    {
        await using var rig = GraphRig.Start(server.Environment);
        var workflow = await rig.Define(GraphRig.Spec("unlimited",
            [GraphRig.Node("start", "manual"), GraphRig.Node("each", "for_each", config: Each(maxItems: 500)), GraphRig.Node("gather", "collect", config: new JsonObject { ["value"] = "{{item}}" })],
            [GraphRig.Edge("start", "each"), GraphRig.Edge("each", "gather")]));

        var started = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited, Trigger(Numbers(500)));
        var run = await rig.Final(started.RunId, _long);
        var history = await rig.Client.GetWorkflowHandle(started.RunId.Value).FetchHistoryAsync();
        var gathered = JsonNode.Parse((await rig.ItemRuns(started.RunId, "gather"))[0].Output!)!;

        Assert.Equal(WorkflowRunStatus.Succeeded, run.Status);
        Assert.Equal(500, gathered["count"]!.GetValue<int>());
        TestContext.Current.SendDiagnosticMessage($"for_each 500 items: parent history {history.Events.Count} events");
        Assert.True(history.Events.Count < 500, $"the parent's history holds {history.Events.Count} events for 500 items");   // why: Temporal stops a workflow at 51,200; the parent holds one entry per batch, not per item
    }

    [Fact]
    public async Task An_empty_list_collects_an_empty_list()
    {
        await using var rig = GraphRig.Start(server.Environment);
        var workflow = await rig.Define(GraphRig.Spec("unlimited",
            [GraphRig.Node("start", "manual"), GraphRig.Node("each", "for_each", config: Each()), GraphRig.Node("gather", "collect", config: new JsonObject { ["value"] = "{{item}}" }), GraphRig.Node("after", "echo")],
            [GraphRig.Edge("start", "each"), GraphRig.Edge("each", "gather"), GraphRig.Edge("gather", "after")]));

        var started = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited, Trigger([]));
        var run = await rig.Final(started.RunId);
        var gathered = JsonNode.Parse((await rig.ItemRuns(started.RunId, "gather"))[0].Output!)!;

        Assert.Equal(WorkflowRunStatus.Succeeded, run.Status);
        Assert.Equal(0, gathered["count"]!.GetValue<int>());
        Assert.Empty(gathered["items"]!.AsArray());
        Assert.Equal([$"{rig.Slug}@1:1/after"], rig.Blocks.Log);   // the node after the loop still runs
    }

    [Fact]
    public async Task An_item_that_skips_the_collect_adds_nothing_and_the_others_keep_their_order()
    {
        await using var rig = GraphRig.Start(server.Environment);
        var workflow = await rig.Define(GraphRig.Spec("unlimited",
            [GraphRig.Node("start", "manual"), GraphRig.Node("each", "for_each", config: Each()), GraphRig.Node("keep", "condition", config: new JsonObject { ["when"] = "item.keep" }),
                GraphRig.Node("tag", "pass", config: new JsonObject { ["value"] = "{{item.n}}" }), GraphRig.Node("gather", "collect", config: new JsonObject { ["value"] = "{{tag.value}}" })],
            [GraphRig.Edge("start", "each"), GraphRig.Edge("each", "keep"), GraphRig.Edge("keep", "tag", "true"), GraphRig.Edge("tag", "gather")]));
        JsonNode?[] items = [Item(1, keep: true), Item(2, keep: false), Item(3, keep: true)];

        var started = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited, Trigger(items));
        var run = await rig.Final(started.RunId);
        var gathered = JsonNode.Parse((await rig.ItemRuns(started.RunId, "gather"))[0].Output!)!;
        var gatherRuns = await rig.ItemRuns(started.RunId, "gather");

        Assert.Equal(WorkflowRunStatus.Succeeded, run.Status);
        Assert.Equal([1, 3], gathered["items"]!.AsArray().Select(static n => n!.GetValue<int>()));
        Assert.Equal(2, gathered["count"]!.GetValue<int>());
        Assert.Equal(NodeRunStatus.Skipped, gatherRuns[2].Status);
    }

    [Fact]
    public async Task A_failing_item_stops_the_loop_fails_the_collect_and_skips_what_follows()
    {
        await using var rig = GraphRig.Start(server.Environment);
        var workflow = await rig.Define(GraphRig.Spec("unlimited",
            [GraphRig.Node("start", "manual"), GraphRig.Node("each", "for_each", config: Each()), GraphRig.Node("bad", "fail"), GraphRig.Node("gather", "collect", config: new JsonObject { ["value"] = "{{item}}" }), GraphRig.Node("after", "echo")],
            [GraphRig.Edge("start", "each"), GraphRig.Edge("each", "bad"), GraphRig.Edge("bad", "gather"), GraphRig.Edge("gather", "after")]));

        var started = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited, Trigger(Numbers(3)));
        var run = await rig.Final(started.RunId);
        var badRuns = await rig.ItemRuns(started.RunId, "bad");

        Assert.Equal(WorkflowRunStatus.Failed, run.Status);
        Assert.Contains("item 1", run.Error, StringComparison.Ordinal);
        Assert.Contains("boom", run.Error, StringComparison.Ordinal);
        Assert.Equal(NodeRunStatus.Failed, badRuns[1].Status);
        Assert.False(badRuns.ContainsKey(2));   // the loop stopped at the first failure
        Assert.Equal(NodeRunStatus.Failed, (await rig.ItemRuns(started.RunId, "gather"))[0].Status);
        Assert.Equal(NodeRunStatus.Skipped, (await rig.ItemRuns(started.RunId, "after"))[0].Status);
    }

    [Fact]
    public async Task More_items_than_maxItems_fail_the_for_each_before_any_item_runs()
    {
        await using var rig = GraphRig.Start(server.Environment);
        var workflow = await rig.Define(GraphRig.Spec("unlimited",
            [GraphRig.Node("start", "manual"), GraphRig.Node("each", "for_each", config: Each(maxItems: 2)), GraphRig.Node("tag", "pass", config: new JsonObject { ["value"] = "{{item}}" }), GraphRig.Node("gather", "collect", config: new JsonObject { ["value"] = "{{tag.value}}" })],
            [GraphRig.Edge("start", "each"), GraphRig.Edge("each", "tag"), GraphRig.Edge("tag", "gather")]));

        var started = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited, Trigger(Numbers(3)));
        var run = await rig.Final(started.RunId);
        var each = (await rig.ItemRuns(started.RunId, "each"))[0];

        Assert.Equal(WorkflowRunStatus.Failed, run.Status);
        Assert.Equal(NodeRunStatus.Failed, each.Status);
        Assert.Contains("maxItems 2", each.Error, StringComparison.Ordinal);
        Assert.False((await rig.ItemRuns(started.RunId, "tag")).ContainsKey(1));
    }

    [Fact]
    public async Task A_for_each_with_no_collect_fails_before_it_runs()
    {
        await using var rig = GraphRig.Start(server.Environment);
        var workflow = await rig.Define(GraphRig.Spec("unlimited",
            [GraphRig.Node("start", "manual"), GraphRig.Node("each", "for_each", config: Each()), GraphRig.Node("tag", "pass")],
            [GraphRig.Edge("start", "each"), GraphRig.Edge("each", "tag")]));

        var started = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited, Trigger(Numbers(2)));
        var run = await rig.Final(started.RunId);
        var each = (await rig.ItemRuns(started.RunId, "each"))[0];

        Assert.Equal(WorkflowRunStatus.Failed, run.Status);
        Assert.Equal(NodeRunStatus.Failed, each.Status);
        Assert.Contains("collect", each.Error, StringComparison.Ordinal);
    }

    private static JsonObject Item(int n, bool keep) => new() { ["n"] = n, ["keep"] = keep };
}
