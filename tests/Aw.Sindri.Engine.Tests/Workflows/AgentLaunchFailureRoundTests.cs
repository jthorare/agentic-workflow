using Aw.Kernel.Testing;
using Aw.Sindri.Core;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// A launch that never started a session uses no loop round ('launch-failure-keeps-round-v1'): the Step fails with reason 'launch', the failed transition re-enters the
/// looped state, and the round stays. A new run takes that path, and its history replays (the rig replays it after the body); a history from before the marker is
/// the one the pre-patch replay tests hold, and the decision's legacy flag is in LaunchFailureRoundsTests.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class AgentLaunchFailureRoundTests : AgentRuntimeTestBase
{
    private const string Retrying = "workflow.dev-agent-retry";

    [Fact(Timeout = 120_000)]
    public async Task A_failed_launch_runs_the_same_round_again()
    {
        var ct = TestContext.Current.CancellationToken;
        _root = Retrying;
        _also = [_retry];
        await using var rig = await Rig(ct);
        _launcher.FailNextWith(SindriErrors.LaunchAgentMissing.With("claude is not installed"));
        await rig.Run(async handle =>
        {
            await WaitFor(() => _launcher.Requests.Count == 2, ct);   // the failed launch, then the one that started
            await Task.Delay(TimeSpan.FromSeconds(1), ct);

            var view = await WorkflowsHarness.Until(handle, static v => v.Run is not null && v.State == "write");
            Assert.Contains("round 1/3", view.Next, StringComparison.Ordinal);
        });
    }

    private static readonly string _retry = string.Join('\n',
        "schema: 1",
        "id: " + Retrying,
        "version: 1",
        "description: \"A looped agent Step that retries a failed run (launch failure rounds).\"",
        "inputs:",
        "  - { key: fact:item, label: \"Item\", type: item, kind: item }",
        "  - { key: fact:issue, label: \"Issue\", type: text, kind: text }",
        "initial: write",
        "loops: { rounds: { cap: 3 } }",
        "states:",
        "  write:",
        "    kind: active",
        "    step: step.report.write",
        "    loop: rounds",
        "    budget: { tokens: 1000 }",
        "    timeout: { after: 1m, then: park }",
        "    next: \"Write up the resolution for {item} (round {loop.rounds}/{loop.rounds.cap}).\"",
        "  done: { kind: terminal, outcome: resolved }",
        "transitions:",
        "  - { from: write, to: write, on: step.failed }",
        "  - { from: write, to: done, on: step.completed, when: { outcome: done }, produces: [fact:resolution] }");
}
