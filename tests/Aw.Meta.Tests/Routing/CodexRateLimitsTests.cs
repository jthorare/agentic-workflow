using Aw.Meta.Core.Routing;

namespace Aw.Meta.Tests.Routing;

public sealed class CodexRateLimitsTests
{
    private static readonly DateTimeOffset _now = RoutingWorld.Now;

    [Fact, Trait("Lane", "unit")]
    public void The_last_snapshot_wins_and_its_windows_are_named_by_length()
    {
        var signal = CodexRateLimits.Read([RoutingWorld.Rollout(10, 20), "{\"type\":\"user\"}", RoutingWorld.Rollout(88, 48)], _now);

        Assert.NotNull(signal);
        Assert.Equal(["5h", "weekly"], signal.Windows.Select(w => w.Name), StringComparer.Ordinal);
        Assert.Equal([88d, 48d], signal.Windows.Select(w => w.UsedPct));
        Assert.Equal(_now.AddHours(2), signal.Windows[0].ResetsAt);
        Assert.Equal(12d, signal.RemainingPct);
        Assert.False(signal.LimitReached);
    }

    [Fact, Trait("Lane", "unit")]
    public void Lines_without_a_snapshot_and_damaged_lines_are_skipped_and_none_at_all_is_null()
    {
        const string NullLimits = """{"type":"event_msg","payload":{"type":"token_count","info":null,"rate_limits":null}}""";

        Assert.Null(CodexRateLimits.Read([NullLimits, "not json", """{"type":"user"}"""], _now));
        Assert.Equal(52d, CodexRateLimits.Read([RoutingWorld.Rollout(48, 10), NullLimits, "{\"rate_limits\":"], _now)?.RemainingPct);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_window_whose_reset_time_has_passed_counts_as_unused()
    {
        var signal = CodexRateLimits.Read([RoutingWorld.Rollout(97, 30, primaryResets: _now.AddMinutes(-5))], _now);

        Assert.Equal(0d, signal!.Windows[0].UsedPct);
        Assert.Equal(70d, signal.RemainingPct);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_limit_is_reached_when_the_provider_says_so_or_a_current_window_is_full()
    {
        Assert.True(CodexRateLimits.Read([RoutingWorld.Rollout(40, 10, reached: "\"primary\"")], _now)!.LimitReached);
        Assert.True(CodexRateLimits.Read([RoutingWorld.Rollout(100, 10)], _now)!.LimitReached);
        Assert.False(CodexRateLimits.Read([RoutingWorld.Rollout(100, 10, primaryResets: _now.AddMinutes(-1))], _now)!.LimitReached);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_reached_type_that_names_an_expired_window_is_not_a_limit_while_the_other_window_is_current()
    {
        var line = RoutingWorld.Rollout(100, 20, primaryResets: _now.AddHours(-1), reached: "\"primary\"");

        Assert.False(CodexRateLimits.Read([line], _now)!.LimitReached);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_reached_type_that_names_no_window_is_dropped_once_any_window_has_expired()
    {
        var line = RoutingWorld.Rollout(100, 20, primaryResets: _now.AddHours(-1), reached: "\"rate_limit\"");

        Assert.False(CodexRateLimits.Read([line], _now)!.LimitReached);
        Assert.True(CodexRateLimits.Read([RoutingWorld.Rollout(100, 20, reached: "\"rate_limit\"")], _now)!.LimitReached);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_reset_time_in_milliseconds_gives_no_figure_for_that_window_and_does_not_throw()
    {
        var line = RoutingWorld.Rollout(40, 20).Replace(_now.AddHours(2).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture), _now.AddHours(2).ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);

        var signal = CodexRateLimits.Read([line], _now);

        Assert.Equal(["weekly"], signal!.Windows.Select(w => w.Name), StringComparer.Ordinal);
    }
}
