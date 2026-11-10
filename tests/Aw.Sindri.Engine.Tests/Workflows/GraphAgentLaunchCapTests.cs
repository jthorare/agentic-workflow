using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Runs;

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

    // why: the lead sets the production cap from N, the history events one failed attempt adds; two runs with different caps give it as a difference (small caps: the back-off is real time, 100 ms doubling to 30 s)
    [Fact]
    public async Task A_failed_attempt_adds_a_constant_number_of_history_events()
    {
        var few = await HistoryLength(4);
        var many = await HistoryLength(8);

        var perAttempt = (many - few) / 4;
        var text = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"N = {perAttempt} ({few} events at 4 attempts, {many} at 8; the 4 extra attempts added {many - few})");
        TestContext.Current.SendDiagnosticMessage("history events per failed launch attempt: " + text);
        Assert.True((many - few) % 4 == 0, "not a whole number of events per attempt: " + text);
        Assert.InRange(perAttempt, 1, 20);
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
