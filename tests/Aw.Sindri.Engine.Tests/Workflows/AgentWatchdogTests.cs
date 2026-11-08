using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Features.SweepLostSessions;
using Aw.Sindri.Infrastructure.Workflows;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>The watchdog's verdict reaches the 'run_agent' node as its error: the idle sweep ends the session, and the node fails naming why.</summary>
[Lane(TestLane.Unit)]
public sealed class AgentWatchdogTests
{
    private static readonly DateTimeOffset _t0 = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_session_idle_past_its_limit_fails_the_node_with_idle()
    {
        var ended = _t0;
        var swept = IdleRules.Decide(new IdleInputs(_t0 + LaunchRules.IdleAfter, ended, TurnFailed: null, SnapshotIdleSince: null, ToolRunning: false, Nudged: null), TimeSpan.FromMinutes(5));   // contract: passes today
        var error = AgentNodeFailure.Of(new AgentSessionState(AgentSessionEnd.Idle, Detail: "no report after the turn ended"), timedOut: false);

        Assert.Equal(IdleAction.End, swept);
        Assert.Contains("idle", error, StringComparison.Ordinal);
        Assert.Contains("no report after the turn ended", error, StringComparison.Ordinal);
    }

    [Fact]
    public void A_lost_session_fails_the_node_with_lost()
    {
        var error = AgentNodeFailure.Of(new AgentSessionState(AgentSessionEnd.Lost), timedOut: false);

        Assert.Contains("lost", error, StringComparison.Ordinal);
    }

    [Fact]
    public void A_node_whose_timeout_ran_out_fails_with_timeout_even_though_the_session_is_still_live()
    {
        var error = AgentNodeFailure.Of(AgentSessionState.Live, timedOut: true);

        Assert.Contains("timeout", error, StringComparison.Ordinal);
    }
}
