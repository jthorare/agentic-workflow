using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Infrastructure.Workflows;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>A 'run_agent' node in the graph workflow (PR 7): launch, the report that ends it, recovery from the output folder, and a launch that fails.</summary>
[Lane(TestLane.Integration)]
public sealed class GraphAgentNodeTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_report_ends_the_node_with_the_declared_outputs_and_the_standard_fields()
    {
        var runtime = new FakeAgentNodeRuntime();
        await using var rig = await GraphRig.StartAsync(runtime);
        var run = await AgentNodeRig.Start(rig, """{"drifted":"boolean"}""");
        await runtime.Watching.WaitAsync(TimeSpan.FromSeconds(30), Token);

        var delivered = await rig.Control.Report(run, new AgentNodeReport(AgentNodeRig.Agent, ItemIndex: 0, """{"drifted":true}""", Human: false), Token);
        var final = await rig.Final(run);

        Assert.True(delivered.IsSuccess, delivered.IsError ? delivered.Error.Message : null);
        Assert.Equal(WorkflowRunStatus.Succeeded, final.Status);
        var row = await AgentNodeRig.Row(rig, run);
        Assert.Equal(NodeRunStatus.Succeeded, row.Status);
        var output = JsonNode.Parse(row.Output!)!;
        Assert.True(output["drifted"]!.GetValue<bool>());
        Assert.Equal("succeeded", output["status"]!.GetValue<string>());
        Assert.Equal($"wf/{rig.Slug}/1-1-agent/work", output["branch"]!.GetValue<string>());
        Assert.Equal("def4567", output["commit"]!.GetValue<string>());
        Assert.Equal("agent-session-1", output["sessionId"]!.GetValue<string>());
        Assert.Equal(100, output["usage"]!["tokens"]!.GetValue<int>());
        var launch = NodeRunAddress.Of(run, AgentNodeRig.Agent).Launch().Value;
        Assert.Equal(launch, Assert.Single(runtime.Closes).Launch);
        Assert.True(runtime.Closes[0].Measure);
        _ = Assert.Single(runtime.Launches);
    }

    [Fact]
    public async Task A_launch_that_fails_fails_the_node_with_its_code_and_starts_no_watch()
    {
        var runtime = new FakeAgentNodeRuntime { Failure = new AgentLaunchFailure(AgentLaunchFailureKind.StepFailed, "SND-LAUNCH-004", "the repo is not in the profile") };
        await using var rig = await GraphRig.StartAsync(runtime);
        var run = await AgentNodeRig.Start(rig);

        var final = await rig.Final(run);

        Assert.Equal(WorkflowRunStatus.Failed, final.Status);
        var row = await AgentNodeRig.Row(rig, run);
        Assert.Equal(NodeRunStatus.Failed, row.Status);
        Assert.Contains("SND-LAUNCH-004", row.Error, StringComparison.Ordinal);
        Assert.False(runtime.Watching.IsCompleted);
    }

    [Fact]
    public async Task A_session_that_ended_with_its_outputs_in_the_folder_completes_the_node()
    {
        var runtime = new FakeAgentNodeRuntime { Recovered = new AgentNodeRecovered("""{"drifted":false}""", Reason: null) };
        await using var rig = await GraphRig.StartAsync(runtime);
        var run = await AgentNodeRig.Start(rig, """{"drifted":"boolean"}""");
        await runtime.Watching.WaitAsync(TimeSpan.FromSeconds(30), Token);

        runtime.End(new AgentSessionState(AgentSessionEnd.Exit, ExitCode: 0));
        var final = await rig.Final(run);

        Assert.Equal(WorkflowRunStatus.Succeeded, final.Status);
        Assert.False(JsonNode.Parse((await AgentNodeRig.Row(rig, run)).Output!)!["drifted"]!.GetValue<bool>());
    }

    [Fact]
    public async Task A_session_that_ended_with_nothing_to_recover_fails_the_node_with_exit_and_the_reason()
    {
        var runtime = new FakeAgentNodeRuntime { Recovered = new AgentNodeRecovered(Outputs: null, Reason: "the agent finished but its result was not recorded: /home/nodes/run/.sindri-out") };
        await using var rig = await GraphRig.StartAsync(runtime);
        var run = await AgentNodeRig.Start(rig, """{"drifted":"boolean"}""");
        await runtime.Watching.WaitAsync(TimeSpan.FromSeconds(30), Token);

        runtime.End(new AgentSessionState(AgentSessionEnd.Exit, ExitCode: 1));
        var final = await rig.Final(run);

        Assert.Equal(WorkflowRunStatus.Failed, final.Status);
        var error = (await AgentNodeRig.Row(rig, run)).Error;
        Assert.Contains("exit", error, StringComparison.Ordinal);
        Assert.Contains(".sindri-out", error, StringComparison.Ordinal);
        Assert.True(Assert.Single(runtime.Closes).Measure);
    }
}
