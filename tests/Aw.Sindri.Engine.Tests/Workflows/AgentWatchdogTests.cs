using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Infrastructure.Workflows;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>How a 'run_agent' node fails when its session is ended for it (PR 7): the sweeps end the session, the watch reports how, and the node's error says which reason.</summary>
[Lane(TestLane.Integration)]
public sealed class AgentWatchdogTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<string?> ErrorOf(AgentSessionState ended, AgentNodeRecovered? recovered = null)
    {
        var runtime = new FakeAgentNodeRuntime();
        if (recovered is not null)
        {
            runtime.Recovered = recovered;
        }

        await using var rig = await GraphRig.StartAsync(runtime);
        var run = await AgentNodeRig.Start(rig);
        await runtime.Watching.WaitAsync(TimeSpan.FromSeconds(30), Token);

        runtime.End(ended);
        var final = await rig.Final(run);

        Assert.Equal(WorkflowRunStatus.Failed, final.Status);
        Assert.Equal(NodeRunStatus.Failed, (await AgentNodeRig.Row(rig, run)).Status);
        return (await AgentNodeRig.Row(rig, run)).Error;
    }

    [Fact]
    public async Task A_session_idle_past_its_limit_fails_the_node_with_idle()
    {
        var error = await ErrorOf(new AgentSessionState(AgentSessionEnd.Idle));

        Assert.Contains("idle", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_idle_session_whose_commands_the_sandbox_refused_names_the_refusals_in_the_error()
    {
        const string refused = "the sandbox refused 4 actions (Bash: gh run x3, Bash: gh auth)";

        var error = await ErrorOf(new AgentSessionState(AgentSessionEnd.Idle), new AgentNodeRecovered(Outputs: null, Reason: null, Denials: refused));

        Assert.Contains("idle: the agent sat idle at its prompt with no report", error, StringComparison.Ordinal);
        Assert.Contains(refused, error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_session_that_stopped_heartbeating_fails_the_node_with_lost()
    {
        var error = await ErrorOf(new AgentSessionState(AgentSessionEnd.Lost));

        Assert.Contains("lost", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_session_a_sandbox_limit_killed_fails_the_node_with_limit_and_the_limit()
    {
        var error = await ErrorOf(new AgentSessionState(AgentSessionEnd.Limit, Limit: new AgentSessionLimit(AgentLimitKind.Memory, Limit: 2_147_483_648, Peak: 2_300_000_000)));

        Assert.Contains("limit", error, StringComparison.Ordinal);
        Assert.Contains("2147483648", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_report_the_session_was_refused_for_fails_the_node_with_the_refusal()
    {
        var error = await ErrorOf(new AgentSessionState(AgentSessionEnd.Exit, ExitCode: 0, Detail: "the report named an output the node does not declare", Refused: true));

        Assert.Contains("does not declare", error, StringComparison.Ordinal);
    }
}
