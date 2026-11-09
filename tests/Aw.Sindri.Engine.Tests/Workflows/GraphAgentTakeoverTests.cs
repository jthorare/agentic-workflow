using System.Diagnostics;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>Takeover and handback of a 'run_agent' node (PR 7): the timeout pauses while a person holds the node and resumes with what was left; the agent may not report a taken-over node.</summary>
[Lane(TestLane.Integration)]
public sealed class GraphAgentTakeoverTests(SindriTimeSkippingServer server) : IClassFixture<SindriTimeSkippingServer>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // why: the watch is an activity in flight, and the test server does not skip time while one is, so this runs in real time with a short timeout the fake launch reports
    [Fact]
    public async Task A_takeover_pauses_the_node_timeout_and_handback_resumes_the_remainder()
    {
        var runtime = new FakeAgentNodeRuntime { Timeout = TimeSpan.FromSeconds(6) };
        await using var rig = GraphRig.Start(server.Environment, runtime);
        var run = await AgentNodeRig.Start(rig);
        await runtime.Watching.WaitAsync(TimeSpan.FromSeconds(30), Token);
        var clock = Stopwatch.StartNew();

        await Task.Delay(TimeSpan.FromSeconds(1), Token);
        var tookOver = await rig.Control.Takeover(run, AgentNodeRig.Agent, itemIndex: 0, Token);
        await Task.Delay(TimeSpan.FromSeconds(7), Token);   // longer than the whole timeout
        var stillRunning = (await rig.Runs.Get(run, Token)).Value.Status;
        var handedBack = clock.Elapsed;
        var back = await rig.Control.Handback(run, AgentNodeRig.Agent, itemIndex: 0, Token);
        var final = await rig.Final(run);
        var afterHandback = clock.Elapsed - handedBack;

        Assert.True(tookOver.IsSuccess, tookOver.IsError ? tookOver.Error.Message : null);
        Assert.True(back.IsSuccess, back.IsError ? back.Error.Message : null);
        Assert.Equal(WorkflowRunStatus.Running, stillRunning);   // the timeout did not run while the node was held
        Assert.Equal(WorkflowRunStatus.Failed, final.Status);
        Assert.Contains("timeout", (await AgentNodeRig.Row(rig, run)).Error, StringComparison.Ordinal);
        Assert.InRange(afterHandback, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(5.8));   // about 5 s were left of 6, not 6 and not 0
        Assert.Equal([true, false], runtime.Holds);
        _ = Assert.Single(runtime.Closes);
    }

    [Fact]
    public async Task The_agent_s_report_of_a_taken_over_node_is_refused_and_the_human_s_completes_it()
    {
        var runtime = new FakeAgentNodeRuntime();
        await using var rig = GraphRig.Start(server.Environment, runtime);
        var run = await AgentNodeRig.Start(rig, """{"ok":"boolean"}""");
        await runtime.Watching.WaitAsync(TimeSpan.FromSeconds(30), Token);
        _ = await rig.Control.Takeover(run, AgentNodeRig.Agent, itemIndex: 0, Token);

        var agent = await rig.Control.Report(run, new AgentNodeReport(AgentNodeRig.Agent, 0, """{"ok":true}""", Human: false), Token);
        var human = await rig.Control.Report(run, new AgentNodeReport(AgentNodeRig.Agent, 0, """{"ok":true}""", Human: true), Token);
        var final = await rig.Final(run);

        Assert.True(agent.IsError);
        Assert.Contains("taken over", agent.Error.Message, StringComparison.Ordinal);
        Assert.True(human.IsSuccess, human.IsError ? human.Error.Message : null);
        Assert.Equal(WorkflowRunStatus.Succeeded, final.Status);
        Assert.True(JsonNode.Parse((await AgentNodeRig.Row(rig, run)).Output!)!["ok"]!.GetValue<bool>());
    }

    [Fact]
    public async Task A_handback_of_a_node_nobody_holds_and_a_takeover_of_another_node_are_refused()
    {
        var runtime = new FakeAgentNodeRuntime();
        await using var rig = GraphRig.Start(server.Environment, runtime);
        var run = await AgentNodeRig.Start(rig);
        await runtime.Watching.WaitAsync(TimeSpan.FromSeconds(30), Token);

        var back = await rig.Control.Handback(run, AgentNodeRig.Agent, itemIndex: 0, Token);
        var other = await rig.Control.Takeover(run, NodeId.From("elsewhere"), itemIndex: 0, Token);

        Assert.True(back.IsError);
        Assert.True(other.IsError);
        runtime.End(new AgentSessionState(AgentSessionEnd.Exit, ExitCode: 0));
    }

    [Fact]
    public async Task The_run_names_the_node_it_waits_on_and_whether_it_is_held()
    {
        var runtime = new FakeAgentNodeRuntime();
        await using var rig = GraphRig.Start(server.Environment, runtime);
        var run = await AgentNodeRig.Start(rig);
        await runtime.Watching.WaitAsync(TimeSpan.FromSeconds(30), Token);

        var before = (await rig.Control.Running(run, Token)).Value;
        _ = await rig.Control.Takeover(run, AgentNodeRig.Agent, itemIndex: 0, Token);
        var held = (await rig.Control.Running(run, Token)).Value;

        Assert.Equal(AgentNodeRig.Agent, before!.Node);
        Assert.Equal(NodeRunAddress.Of(run, AgentNodeRig.Agent).Launch(), before.Launch);
        Assert.False(before.TakenOver);
        Assert.True(held!.TakenOver);
        _ = await rig.Control.Handback(run, AgentNodeRig.Agent, itemIndex: 0, Token);
        runtime.End(new AgentSessionState(AgentSessionEnd.Exit, ExitCode: 0));
    }

    [Fact]
    public async Task A_cancel_stops_the_session_without_measuring_it()
    {
        var runtime = new FakeAgentNodeRuntime();
        await using var rig = GraphRig.Start(server.Environment, runtime);
        var run = await AgentNodeRig.Start(rig);
        await runtime.Watching.WaitAsync(TimeSpan.FromSeconds(30), Token);

        await rig.Client.GetWorkflowHandle(run.Value).CancelAsync();
        var final = await rig.Final(run);

        Assert.Equal(WorkflowRunStatus.Cancelled, final.Status);
        Assert.Equal(NodeRunStatus.Cancelled, (await AgentNodeRig.Row(rig, run)).Status);
        Assert.False(Assert.Single(runtime.Closes).Measure);
    }
}
