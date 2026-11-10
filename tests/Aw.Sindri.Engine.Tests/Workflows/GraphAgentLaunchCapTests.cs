using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Infrastructure.Workflows;
using Temporalio.Common;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>A 'run_agent' launch that keeps throwing is tried a bounded number of times, then the node fails with the last error (#2578): the workflow's history grows with every attempt.</summary>
[Lane(TestLane.Integration)]
public sealed class GraphAgentLaunchCapTests
{
    [Fact]
    public async Task A_launch_that_keeps_throwing_fails_the_node_with_its_last_error_after_the_cap_and_the_run_ends_in_the_ledger()
    {
        var runtime = new FakeAgentNodeRuntime { LaunchThrows = new InvalidOperationException("store error") };
        await using var rig = await GraphRig.StartAsync(runtime, agentLaunchAttempts: 3);

        var run = await AgentNodeRig.Start(rig);
        var final = await rig.Final(run);
        var rows = await rig.RowsWith($"workflow.run.finished workflow.run.finished:{run.Value}");

        Assert.Equal(WorkflowRunStatus.Failed, final.Status);
        var row = await AgentNodeRig.Row(rig, run);
        Assert.Equal(NodeRunStatus.Failed, row.Status);
        Assert.Contains("store error", row.Error, StringComparison.Ordinal);
        Assert.Contains($"workflow.run.finished workflow.run.finished:{run.Value}", rows, StringComparer.Ordinal);
        Assert.Equal(3, runtime.Launches.Count);
    }

    // why: a 'for_each' batch runs its items' agents in its own workflow (#2470), so the run's cap must reach that workflow too; without it each item would retry to the default 130
    [Fact]
    public async Task An_item_launch_in_a_for_each_body_that_keeps_throwing_stops_at_the_runs_cap()
    {
        var runtime = new FakeAgentNodeRuntime { LaunchThrows = new InvalidOperationException("store error") };
        await using var rig = await GraphRig.StartAsync(runtime, agentLaunchAttempts: 3);
        var workflow = await rig.Define(GraphRig.Spec("unlimited",
            [GraphRig.Node("start", "manual"), GraphRig.Node("each", "for_each", config: new JsonObject { ["items"] = "{{trigger.items}}" }),
                GraphRig.Node("work", "run_agent", config: new JsonObject { ["repo"] = "web", ["prompt"] = "{{item}}" }), GraphRig.Node("gather", "collect", config: new JsonObject { ["value"] = "{{work.status}}" })],
            [GraphRig.Edge("start", "each"), GraphRig.Edge("each", "work"), GraphRig.Edge("work", "gather")]));

        var started = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited, new JsonObject { ["kind"] = "manual", ["items"] = new JsonArray("a", "b") }.ToJsonString());
        var final = await rig.Final(started.RunId);
        var rows = await rig.ItemRuns(started.RunId, "work");

        Assert.Equal(WorkflowRunStatus.Failed, final.Status);
        var perItem = runtime.Launches.GroupBy(static l => l.ItemIndex).ToList();
        Assert.NotEmpty(perItem);
        Assert.All(perItem, static g => Assert.Equal(3, g.Count()));
        Assert.All(perItem, g => Assert.Equal(NodeRunStatus.Failed, rows[g.Key].Status));
        Assert.All(perItem, g => Assert.Contains("store error", rows[g.Key].Error, StringComparison.Ordinal));
    }

    // why: bounds the history events one failed attempt adds, which is what keeps the cap's attempts far under Temporal's 51,200 events; the count wobbles by one or two (Temporal folds an attempt's events into a shared workflow task), so it is an upper bound, not a constant
    [Fact]
    public async Task A_failed_attempt_adds_at_most_20_history_events()
    {
        var few = await HistoryLength(4);
        var many = await HistoryLength(8);

        var perAttempt = (int)Math.Ceiling((many - few) / 4.0);
        var text = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"N = {perAttempt} ({few} events at 4 attempts, {many} at 8; the 4 extra attempts added {many - few})");
        TestContext.Current.SendDiagnosticMessage("history events per failed launch attempt: " + text);
        Assert.InRange(perAttempt, 1, 20);
    }

    // why: a live run whose launch failed past the cap before the cap existed holds a back-off timer where the new code would fail the node; replaying it must not raise nondeterminism (Workflow.Patched("agent-launch-cap-1")).
    // The history is recorded under a large cap, so it holds 6 attempts and no marker, and the cap in its start input is then lowered to 3 so the replay reaches the cap inside the recorded attempts
    [Fact]
    public async Task A_history_recorded_past_the_cap_without_the_cap_marker_replays_without_nondeterminism()
    {
        var runtime = new FakeAgentNodeRuntime { LaunchThrows = new InvalidOperationException("store error") };
        await using var rig = await GraphRig.StartAsync(runtime, agentLaunchAttempts: 100);
        var run = await AgentNodeRig.Start(rig);
        using (var limit = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken))
        {
            limit.CancelAfter(TimeSpan.FromSeconds(30));
            while (runtime.Launches.Count < 6)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50), limit.Token);
            }
        }

        var handle = rig.Client.GetWorkflowHandle(run.Value);
        var recorded = await handle.FetchHistoryAsync();
        await handle.CancelAsync();
        _ = await rig.Final(run);

        var replayer = new WorkflowReplayer(new WorkflowReplayerOptions().AddWorkflow<GraphWorkflow>());
        var result = await replayer.ReplayWorkflowAsync(WithCap(recorded, 3), throwOnReplayFailure: false, TestContext.Current.CancellationToken);

        Assert.Null(result.ReplayFailure);
    }

    // Rewrites the 'AgentLaunchAttempts' of the workflow's start input in the recorded history.
    private static WorkflowHistory WithCap(WorkflowHistory history, int cap)
    {
        var json = JsonNode.Parse(history.ToJson())!;
        var started = json["events"]!.AsArray().First(static e => e!["workflowExecutionStartedEventAttributes"] is not null)!["workflowExecutionStartedEventAttributes"]!;
        var payload = started["input"]!["payloads"]![0]!;
        var input = JsonNode.Parse(Convert.FromBase64String(payload["data"]!.GetValue<string>()))!.AsObject();
        var key = input.Select(static p => p.Key).First(static k => string.Equals(k, "agentLaunchAttempts", StringComparison.OrdinalIgnoreCase));
        input[key] = cap;
        payload["data"] = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(input.ToJsonString()));
        return WorkflowHistory.FromJson(history.Id, json.ToJsonString());
    }

    private static async Task<int> HistoryLength(int attempts)
    {
        var runtime = new FakeAgentNodeRuntime { LaunchThrows = new InvalidOperationException("store error") };
        await using var rig = await GraphRig.StartAsync(runtime, attempts);
        var run = await AgentNodeRig.Start(rig);
        _ = await rig.Final(run, TimeSpan.FromSeconds(60));
        var history = await rig.Client.GetWorkflowHandle(run.Value).FetchHistoryAsync();
        return history.Events.Count;
    }
}
