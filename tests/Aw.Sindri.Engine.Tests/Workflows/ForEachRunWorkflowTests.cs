using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>'run_workflow' inside a 'for_each' on the time-skipping server (PR 9): each item starts its own callee run, keyed by the item's position.</summary>
[Lane(TestLane.Integration)]
public sealed class ForEachRunWorkflowTests(SindriTimeSkippingServer server) : IClassFixture<SindriTimeSkippingServer>
{
    private static readonly string[] _items = ["a", "b", "c"];

    [Fact]
    public async Task A_run_workflow_inside_for_each_starts_one_callee_run_per_item()
    {
        await using var rig = GraphRig.Start(server.Environment);
        var callee = await rig.DefineCallee(GraphRig.Spec("unlimited", [GraphRig.Node("start", "workflow_call"), GraphRig.Node("a", "echo")], [GraphRig.Edge("start", "a")]));
        var workflow = await rig.Define(GraphRig.Spec("unlimited",
            [
                GraphRig.Node("start", "manual"),
                GraphRig.Node("each", "for_each", config: new JsonObject { ["items"] = "{{trigger.items}}" }),
                GraphRig.Node("call", "run_workflow", config: new JsonObject { ["workflow"] = callee, ["wait"] = true, ["payload"] = new JsonObject { ["item"] = "{{item}}" } }),
            ],
            [GraphRig.Edge("start", "each"), GraphRig.Edge("each", "call")]));
        var trigger = new JsonObject { ["kind"] = "manual", ["items"] = new JsonArray("a", "b", "c") }.ToJsonString();

        var started = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited, trigger);
        var run = await rig.Final(started.RunId, TimeSpan.FromMinutes(2));
        var callees = (await rig.Runs.Recent(WorkflowId.From("WF-00000000000000000000000000000002"), 0, 50, TestContext.Current.CancellationToken)).Value;

        Assert.Equal(WorkflowRunStatus.Succeeded, run.Status);
        Assert.Equal(3, callees.Count);
        Assert.Equal(3, callees.Select(static c => c.Id.Value).Distinct(StringComparer.Ordinal).Count());
        var triggers = callees.Select(static c => JsonNode.Parse(c.Trigger)!.AsObject()).OrderBy(static t => t["index"]!.GetValue<int>()).ToList();
        Assert.Equal([1, 2, 3], triggers.Select(static t => t["index"]!.GetValue<int>()));   // an item's index counts from 1 inside a loop
        Assert.All(triggers, t => Assert.Equal(run.Id.Value, t["parentRun"]!.GetValue<string>()));
        Assert.Equal(_items, triggers.Select(static t => t["payload"]!["item"]!.GetValue<string>()), StringComparer.Ordinal);
        // why: a repeat of one call is one run (GraphWorkflowCallerTests.The_same_call_twice_is_one_run_and_another_index_is_another_run), so a retried activity adds no callee run
    }
}
