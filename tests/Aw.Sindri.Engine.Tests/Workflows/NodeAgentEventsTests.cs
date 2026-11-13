using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Infrastructure.Workflows;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// A 'run_agent' node records its session as 'workflow.node.agent.changed' events (#3151, Agents page E1): the state, what it waits on, the launch, the branch and the timeout deadline.
/// These run on the Temporal rig like the other agent-node tests, so they are integration lane.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class NodeAgentEventsTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Text(JsonObject payload, string name) =>
        payload.FirstOrDefault(p => string.Equals(p.Key, name, StringComparison.OrdinalIgnoreCase)).Value?.GetValue<string>() ?? string.Empty;

    private static string[] States(IReadOnlyList<JsonObject> events) => [.. events.Select(static e => Text(e, "state"))];

    private static async Task<IReadOnlyList<JsonObject>> EventsUntil(GraphRig rig, Func<IReadOnlyList<JsonObject>, bool> done)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(Token);
        limit.CancelAfter(TimeSpan.FromSeconds(30));
        var events = await rig.Payloads(EventTypes.WorkflowNodeAgentChanged);
        while (!done(events) && !limit.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(50), Token);
            events = await rig.Payloads(EventTypes.WorkflowNodeAgentChanged);
        }

        return events;
    }

    [Fact]
    public async Task A_run_agent_node_records_slot_wait_launch_work_and_success_in_order()
    {
        var runtime = new FakeAgentNodeRuntime { SlotWaits = 1 };
        await using var rig = await GraphRig.StartAsync(runtime);
        var run = await AgentNodeRig.Start(rig);
        await runtime.Watching.WaitAsync(TimeSpan.FromSeconds(30), Token);

        _ = await rig.Control.Report(run, new AgentNodeReport(AgentNodeRig.Agent, ItemIndex: 0, "{}", Human: false), Token);
        _ = await rig.Final(run);
        var events = await EventsUntil(rig, static e => e.Count > 0 && Text(e[^1], "state").StartsWith("ended-", StringComparison.Ordinal));

        Assert.Equal([NodeAgentStates.WaitingForSlot, NodeAgentStates.Launching, NodeAgentStates.Working, NodeAgentStates.EndedSucceeded], States(events));
        Assert.StartsWith("launch slot (", Text(events[0], "waitingOn"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_launched_node_carries_its_launch_its_work_branch_and_a_deadline_one_timeout_after_launch()
    {
        var timeout = TimeSpan.FromMinutes(10);
        var runtime = new FakeAgentNodeRuntime { Timeout = timeout };
        await using var rig = await GraphRig.StartAsync(runtime);
        var before = RealTime.NowOffset;
        var run = await AgentNodeRig.Start(rig);
        await runtime.Watching.WaitAsync(TimeSpan.FromSeconds(30), Token);
        var after = RealTime.NowOffset;

        var events = await EventsUntil(rig, static e => e.Any(x => Text(x, "state") == NodeAgentStates.Working));
        var working = events.Last(static e => Text(e, "state") == NodeAgentStates.Working);

        Assert.Equal(NodeRunAddress.Of(run, AgentNodeRig.Agent).Launch().Value, Text(working, "launchId"));
        Assert.Equal($"wf/{rig.Slug}/1-1-agent/work", Text(working, "branch"));
        var deadline = DateTimeOffset.Parse(Text(working, "deadline"), System.Globalization.CultureInfo.InvariantCulture);
        Assert.InRange(deadline, before + timeout, after + timeout + TimeSpan.FromMinutes(1));
    }

    // why: the watch is an activity in flight and the test server does not skip time while one is, so this runs in real time with a short timeout the fake launch reports
    [Fact]
    public async Task A_node_that_runs_past_its_timeout_ends_timed_out()
    {
        var runtime = new FakeAgentNodeRuntime { Timeout = TimeSpan.FromSeconds(3), AskEvery = TimeSpan.FromSeconds(1) };
        await using var rig = await GraphRig.StartAsync(runtime);
        var run = await AgentNodeRig.Start(rig);
        await runtime.Watching.WaitAsync(TimeSpan.FromSeconds(30), Token);
        var clock = System.Diagnostics.Stopwatch.StartNew();

        _ = await rig.Final(run);
        var ranFor = clock.Elapsed;
        var events = await EventsUntil(rig, static e => e.Any(x => Text(x, "state").StartsWith("ended-", StringComparison.Ordinal)));

        Assert.Equal(NodeAgentStates.EndedTimedOut, Text(events[^1], "state"));
        Assert.True(ranFor >= TimeSpan.FromSeconds(2.9), "the node ended after " + ranFor.TotalSeconds.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " s, before its 3 s timeout");
    }

    // why: the wait counted the slice it had just waited out twice, so a 6 s timeout ended at about 5 s
    [Fact]
    public async Task A_report_just_before_the_timeout_still_succeeds()
    {
        var runtime = new FakeAgentNodeRuntime { Timeout = TimeSpan.FromSeconds(6), AskEvery = TimeSpan.FromSeconds(1) };
        await using var rig = await GraphRig.StartAsync(runtime);
        var run = await AgentNodeRig.Start(rig);
        await runtime.Watching.WaitAsync(TimeSpan.FromSeconds(30), Token);

        await Task.Delay(TimeSpan.FromSeconds(5.5), Token);
        _ = await rig.Control.Report(run, new AgentNodeReport(AgentNodeRig.Agent, ItemIndex: 0, "{}", Human: false), Token);
        var final = await rig.Final(run);

        Assert.Equal(WorkflowRunStatus.Succeeded, final.Status);
    }

    [Fact]
    public async Task A_node_whose_agent_exits_with_nothing_to_recover_ends_failed()
    {
        var runtime = new FakeAgentNodeRuntime { Recovered = new AgentNodeRecovered(Outputs: null, Reason: "the agent finished but its result was not recorded") };
        await using var rig = await GraphRig.StartAsync(runtime);
        var run = await AgentNodeRig.Start(rig);
        await runtime.Watching.WaitAsync(TimeSpan.FromSeconds(30), Token);

        runtime.End(new AgentSessionState(AgentSessionEnd.Exit, ExitCode: 1));
        _ = await rig.Final(run);
        var events = await EventsUntil(rig, static e => e.Any(x => Text(x, "state").StartsWith("ended-", StringComparison.Ordinal)));

        Assert.Equal(NodeAgentStates.EndedFailed, Text(events[^1], "state"));
    }

    [Fact]
    public async Task A_session_waiting_on_a_permission_prompt_shows_waiting_on_you_with_the_tool_then_working_again()
    {
        var runtime = new FakeAgentNodeRuntime();
        await using var rig = await GraphRig.StartAsync(runtime);
        var run = await AgentNodeRig.Start(rig);
        await runtime.Watching.WaitAsync(TimeSpan.FromSeconds(30), Token);

        runtime.SetAsk("Bash");
        _ = await EventsUntil(rig, static e => e.Any(x => Text(x, "state") == NodeAgentStates.WaitingOnYou));
        runtime.SetAsk(tool: null);
        var back = await EventsUntil(rig, static e => e.Where(x => Text(x, "state") == NodeAgentStates.Working).Skip(1).Any());
        _ = await rig.Control.Report(run, new AgentNodeReport(AgentNodeRig.Agent, ItemIndex: 0, "{}", Human: false), Token);
        _ = await rig.Final(run);
        var events = await EventsUntil(rig, static e => e.Count > 0 && Text(e[^1], "state").StartsWith("ended-", StringComparison.Ordinal));

        Assert.NotEmpty(back);
        Assert.Equal([NodeAgentStates.Launching, NodeAgentStates.Working, NodeAgentStates.WaitingOnYou, NodeAgentStates.Working, NodeAgentStates.EndedSucceeded], States(events));
        var asking = events[2];
        Assert.Equal("permission prompt", Text(asking, "waitingOn"));
        Assert.Equal("Bash", Text(asking, "lastTool"));
    }

    // why: every ask is a timer, an activity and workflow tasks in the run's history, which Temporal caps at 51,200 events; a long node must ask in a few long calls, not one per few seconds
    [Fact]
    public async Task A_node_that_never_asks_makes_only_a_few_ask_calls_over_its_whole_timeout()
    {
        var runtime = new FakeAgentNodeRuntime { Timeout = TimeSpan.FromSeconds(6), AskEvery = TimeSpan.FromMilliseconds(200), AskCap = TimeSpan.FromSeconds(4) };
        await using var rig = await GraphRig.StartAsync(runtime);
        var run = await AgentNodeRig.Start(rig);

        _ = await rig.Final(run);

        Assert.InRange(runtime.AskCalls, 1, 3);
    }

    [Fact]
    public async Task A_cancelled_run_ends_the_series_with_ended_failed()
    {
        var runtime = new FakeAgentNodeRuntime();
        await using var rig = await GraphRig.StartAsync(runtime);
        var run = await AgentNodeRig.Start(rig);
        await runtime.Watching.WaitAsync(TimeSpan.FromSeconds(30), Token);
        _ = await EventsUntil(rig, static e => e.Any(x => Text(x, "state") == NodeAgentStates.Working));

        _ = await rig.Cancel(run, whenParked: false);
        var events = await EventsUntil(rig, static e => e.Count > 0 && Text(e[^1], "state").StartsWith("ended-", StringComparison.Ordinal));

        Assert.Equal(NodeAgentStates.EndedFailed, Text(events[^1], "state"));
    }

    // why: the clock stands still while a person holds the node, so the deadline the Agents page shows has to move by the time it was held
    [Fact]
    public async Task A_handback_moves_the_deadline_by_the_time_the_node_was_held()
    {
        var runtime = new FakeAgentNodeRuntime { Timeout = TimeSpan.FromSeconds(6) };
        await using var rig = await GraphRig.StartAsync(runtime);
        var run = await AgentNodeRig.Start(rig);
        await runtime.Watching.WaitAsync(TimeSpan.FromSeconds(30), Token);
        var first = await EventsUntil(rig, static e => e.Any(x => Text(x, "state") == NodeAgentStates.Working));
        var before = DateTimeOffset.Parse(Text(first.Last(static e => Text(e, "state") == NodeAgentStates.Working), "deadline"), System.Globalization.CultureInfo.InvariantCulture);

        await Task.Delay(TimeSpan.FromSeconds(1), Token);
        _ = await rig.Control.Takeover(run, AgentNodeRig.Agent, itemIndex: 0, Token);
        await Task.Delay(TimeSpan.FromSeconds(2), Token);
        _ = await rig.Control.Handback(run, AgentNodeRig.Agent, itemIndex: 0, Token);
        var events = await EventsUntil(rig, static e => e.Where(x => Text(x, "state") == NodeAgentStates.Working).Skip(1).Any());
        var after = DateTimeOffset.Parse(Text(events.Last(static e => Text(e, "state") == NodeAgentStates.Working), "deadline"), System.Globalization.CultureInfo.InvariantCulture);
        _ = await rig.Control.Report(run, new AgentNodeReport(AgentNodeRig.Agent, ItemIndex: 0, "{}", Human: false), Token);
        _ = await rig.Final(run);

        Assert.InRange(after - before, TimeSpan.FromSeconds(1.5), TimeSpan.FromSeconds(3.5));   // held about 2 s
    }
}
