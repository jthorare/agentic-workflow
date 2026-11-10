using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>A 'run_agent' node in a 'for_each' body (#2470): one agent per item, each launch and each node row carrying the item's position (counted from 1).</summary>
[Lane(TestLane.Integration)]
public sealed class ForEachAgentTests(SindriTimeSkippingServer server) : IClassFixture<SindriTimeSkippingServer>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_run_agent_in_the_body_launches_one_agent_per_item_with_the_items_launch_id()
    {
        var runtime = new FakeAgentNodeRuntime { Recovered = new AgentNodeRecovered("{}", Reason: null) };
        await using var rig = GraphRig.Start(server.Environment, runtime);
        var workflow = await rig.Define(GraphRig.Spec("unlimited",
            [GraphRig.Node("start", "manual"), GraphRig.Node("each", "for_each", config: new JsonObject { ["items"] = "{{trigger.items}}" }),
                GraphRig.Node("work", "run_agent", config: new JsonObject { ["repo"] = "web", ["prompt"] = "{{item}}" }), GraphRig.Node("gather", "collect", config: new JsonObject { ["value"] = "{{work.status}}" })],
            [GraphRig.Edge("start", "each"), GraphRig.Edge("each", "work"), GraphRig.Edge("work", "gather")]));

        var started = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited, new JsonObject { ["kind"] = "manual", ["items"] = new JsonArray("a", "b") }.ToJsonString());
        await runtime.Watching.WaitAsync(TimeSpan.FromSeconds(30), Token);
        runtime.End(new AgentSessionState(AgentSessionEnd.Exit, ExitCode: 0));
        var run = await rig.Final(started.RunId);
        var rows = await rig.ItemRuns(started.RunId, "work");

        Assert.Equal(WorkflowRunStatus.Succeeded, run.Status);
        Assert.Equal([1, 2], runtime.Launches.Select(static l => l.ItemIndex).Order());
        Assert.Equal([NodeRunStatus.Succeeded, NodeRunStatus.Succeeded], [rows[1].Status, rows[2].Status]);
        var launches = runtime.Closes.Select(static c => c.Launch).Order(StringComparer.Ordinal).ToList();
        Assert.Equal([NodeRunAddress.Of(started.RunId, NodeId.From("work"), 1).Launch().Value, NodeRunAddress.Of(started.RunId, NodeId.From("work"), 2).Launch().Value], launches.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void A_node_scope_opened_for_item_two_reads_the_second_item()
    {
        var spec = GraphRig.Spec("unlimited",
            [GraphRig.Node("start", "manual"), GraphRig.Node("each", "for_each", config: new JsonObject { ["items"] = "{{trigger.items}}" }),
                GraphRig.Node("work", "run_agent", config: new JsonObject { ["repo"] = "web", ["prompt"] = "{{item}}" }), GraphRig.Node("gather", "collect", config: new JsonObject { ["value"] = "{{item}}" })],
            [GraphRig.Edge("start", "each"), GraphRig.Edge("each", "work"), GraphRig.Edge("work", "gather")]);
        var run = new WorkflowRun(WorkflowRunId.Of(WorkflowSlug.From("morning-check"), 1, 1), WorkflowId.From("WF-00000000000000000000000000000001"), 1, new JsonObject { ["kind"] = "manual", ["items"] = new JsonArray("a", "b") }.ToJsonString(), WorkflowRunStatus.Running, Outputs: null, Error: null, ItemId: null, DateTimeOffset.UnixEpoch, EndedAt: null);

        var outer = new GraphNodeScope(run, spec, []);
        var (loop, cut) = GraphLoopScopes.Open(outer, spec, NodeId.From("work"), 2);
        var scope = new GraphNodeScope(run, spec, [], loop);

        Assert.Null(cut);
        Assert.Equal("b", scope.Lookup("item")!.GetValue<string>());
        Assert.Equal(2, scope.Lookup("itemIndex")!.GetValue<int>());
    }
}
