using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Features.SweepLostSessions;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.SessionReports;

/// <summary>
/// The idle sweep's decision (#1265): a session that stopped on an API error, or sits idle with no hook to say so, is nudged once and then ended;
/// a session with a tool call in flight is never touched; a session that works again starts the clock over.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class IdleRulesTests
{
    private static readonly TimeSpan _stalled = TimeSpan.FromMinutes(10);
    private static readonly DateTimeOffset _t0 = KnownInstants.Epoch;

    private static IdleAction Decide(TimeSpan after, DateTimeOffset? turnEnded = null, DateTimeOffset? turnFailed = null, DateTimeOffset? idleSince = null, bool tool = false, DateTimeOffset? nudged = null) =>
        IdleRules.Decide(new IdleInputs(_t0 + after, turnEnded, turnFailed, idleSince, tool, nudged), _stalled);

    [Fact]
    public void A_turn_that_ended_with_no_report_ends_after_IdleAfter_with_no_nudge()
    {
        Expect.Equal(IdleAction.None, Decide(LaunchRules.IdleAfter - TimeSpan.FromSeconds(1), turnEnded: _t0));
        Expect.Equal(IdleAction.End, Decide(LaunchRules.IdleAfter, turnEnded: _t0));
    }

    [Fact]
    public void A_turn_an_API_error_ended_is_nudged_after_IdleAfter_not_ended()
    {
        Expect.Equal(IdleAction.None, Decide(LaunchRules.IdleAfter - TimeSpan.FromSeconds(1), turnFailed: _t0));
        Expect.Equal(IdleAction.Nudge, Decide(LaunchRules.IdleAfter, turnFailed: _t0));
    }

    [Fact]
    public void After_the_nudge_a_session_still_idle_StalledAfter_later_ends()
    {
        var nudged = _t0 + LaunchRules.IdleAfter;
        Expect.Equal(IdleAction.None, Decide(LaunchRules.IdleAfter + _stalled - TimeSpan.FromSeconds(1), turnFailed: _t0, nudged: nudged));
        Expect.Equal(IdleAction.End, Decide(LaunchRules.IdleAfter + _stalled, turnFailed: _t0, nudged: nudged));
    }

    [Fact]
    public void The_nudge_is_sent_once_whatever_the_time()
    {
        var nudged = _t0 + LaunchRules.IdleAfter;
        foreach (var minutes in new[] { 3, 5, 12, 30, 600 })
        {
            Assert.NotEqual(IdleAction.Nudge, Decide(TimeSpan.FromMinutes(minutes), turnFailed: _t0, nudged: nudged));
        }
    }

    [Fact]
    public void A_second_failure_after_the_nudge_starts_the_StalledAfter_clock_from_that_failure()
    {
        var nudged = _t0 + TimeSpan.FromMinutes(3);
        var failedAgain = _t0 + TimeSpan.FromMinutes(20);
        Expect.Equal(IdleAction.None, Decide(TimeSpan.FromMinutes(29), turnFailed: failedAgain, nudged: nudged));
        Expect.Equal(IdleAction.End, Decide(TimeSpan.FromMinutes(30), turnFailed: failedAgain, nudged: nudged));
    }

    [Fact]
    public void A_session_the_collector_sees_idle_with_no_hook_marker_is_nudged_at_StalledAfter()
    {
        Expect.Equal(IdleAction.None, Decide(_stalled - TimeSpan.FromSeconds(1), idleSince: _t0));
        Expect.Equal(IdleAction.Nudge, Decide(_stalled, idleSince: _t0));
    }

    [Fact]
    public void A_session_that_works_again_starts_the_clock_over()
    {
        // idle for 9 min, then running (no idle-since), then idle again from minute 9: its clock runs from there
        Expect.Equal(IdleAction.None, Decide(TimeSpan.FromMinutes(9), idleSince: _t0));
        Expect.Equal(IdleAction.None, Decide(TimeSpan.FromMinutes(12), idleSince: null));
        Expect.Equal(IdleAction.None, Decide(TimeSpan.FromMinutes(18), idleSince: _t0 + TimeSpan.FromMinutes(9)));
        Expect.Equal(IdleAction.Nudge, Decide(TimeSpan.FromMinutes(19), idleSince: _t0 + TimeSpan.FromMinutes(9)));
    }

    [Fact]
    public void A_nudged_session_that_works_again_is_left_alone() =>
        Expect.Equal(IdleAction.None, Decide(TimeSpan.FromMinutes(60), idleSince: null, nudged: _t0 + TimeSpan.FromMinutes(10)));

    [Fact]
    public void A_long_tool_call_is_never_nudged_or_ended()
    {
        // a 20-minute 'dotnet test': the collector's transcript estimate says idle after 60 s, and the nudge is long past
        Expect.Equal(IdleAction.None, Decide(TimeSpan.FromMinutes(30), idleSince: _t0, tool: true));
        Expect.Equal(IdleAction.None, Decide(TimeSpan.FromMinutes(30), idleSince: _t0, tool: true, nudged: _t0 + TimeSpan.FromMinutes(10)));
        Expect.Equal(IdleAction.None, Decide(TimeSpan.FromMinutes(30), turnEnded: _t0, tool: true));
    }
}
