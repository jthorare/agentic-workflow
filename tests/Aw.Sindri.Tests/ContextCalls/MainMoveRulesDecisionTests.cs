using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>Issue 1041: a change of rules follows the same running and backoff guards as a head move.</summary>
public sealed class MainMoveRulesDecisionTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);
    private static readonly Dictionary<string, string> _heads = new(StringComparer.Ordinal) { ["a"] = "1" };

    [Fact, Trait("Lane", "unit")]
    public void Changed_rules_wait_for_a_running_job()
    {
        var action = MainMoveDecision.Decide(new MainMoveInput(_heads, _heads, RulesChanged: true), running: true, [], _now);

        Assert.Equal(MainMoveAction.JobRunning, action);
    }

    [Fact, Trait("Lane", "unit")]
    public void Changed_rules_back_off_after_a_recent_failure()
    {
        var failed = new SyncAttempt(Failed: true, _now.AddMinutes(-1));

        var action = MainMoveDecision.Decide(new MainMoveInput(_heads, _heads, RulesChanged: true), running: false, [failed], _now);

        Assert.Equal(MainMoveAction.BackoffNotDue, action);
    }

    [Fact, Trait("Lane", "unit")]
    public void Changed_rules_with_no_repo_on_trunk_do_nothing()
    {
        var action = MainMoveDecision.Decide(new MainMoveInput(new Dictionary<string, string>(StringComparer.Ordinal), _heads, RulesChanged: true), running: false, [], _now);

        Assert.Equal(MainMoveAction.Nothing, action);
    }
}
