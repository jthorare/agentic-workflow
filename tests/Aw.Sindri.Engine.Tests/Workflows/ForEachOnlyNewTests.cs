using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>'only_new' inside a 'for_each' body (PR 9): each item's keys are recorded at the run's end, so the next run passes none of them again.</summary>
[Lane(TestLane.Integration)]
public sealed class ForEachOnlyNewTests(SindriTimeSkippingServer server) : IClassFixture<SindriTimeSkippingServer>
{
    private static readonly int[] _firstCounts = [2, 1];
    private static readonly int[] _secondCounts = [0, 0];

    [Fact]
    public async Task An_only_new_inside_for_each_records_every_items_keys_so_the_next_run_passes_none()
    {
        await using var rig = GraphRig.Start(server.Environment);
        var workflow = await rig.Define(GraphRig.Spec("unlimited",
            [
                GraphRig.Node("start", "manual"),
                GraphRig.Node("each", "for_each", config: new JsonObject { ["items"] = "{{trigger.repos}}" }),
                GraphRig.Node("fresh", "only_new", config: new JsonObject { ["items"] = "{{item.issues}}", ["key"] = "id" }),
                GraphRig.Node("gather", "collect", config: new JsonObject { ["value"] = "{{fresh.count}}" }),
            ],
            [GraphRig.Edge("start", "each"), GraphRig.Edge("each", "fresh"), GraphRig.Edge("fresh", "gather")]));
        var trigger = """{"kind":"manual","repos":[{"issues":[{"id":1},{"id":2}]},{"issues":[{"id":3}]}]}""";

        var first = await rig.Final((await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited, trigger)).RunId, TimeSpan.FromMinutes(2));
        var second = await rig.Final((await rig.Trigger(workflow, occurrence: 2, GraphConcurrency.Unlimited, trigger)).RunId, TimeSpan.FromMinutes(2));

        Assert.Equal(WorkflowRunStatus.Succeeded, first.Status);
        Assert.Equal(WorkflowRunStatus.Succeeded, second.Status);
        Assert.Equal(3, rig.Seen.Count);
        Assert.Equal(_firstCounts, await Counts(rig, first.Id));
        Assert.Equal(_secondCounts, await Counts(rig, second.Id));
    }

    private static async Task<int[]> Counts(GraphRig rig, WorkflowRunId run)
    {
        var rows = (await rig.Runs.NodeRuns(run, TestContext.Current.CancellationToken)).Value;
        return [.. rows.Where(static r => r.NodeId.Value == "fresh" && r.ItemIndex > 0).OrderBy(static r => r.ItemIndex).Select(static r => JsonNode.Parse(r.Output!)!["count"]!.GetValue<int>())];
    }
}
