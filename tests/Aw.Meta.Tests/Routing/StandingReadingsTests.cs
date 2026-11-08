using Aw.Meta.Core.Routing;
using Aw.Meta.Core.Usage;
using static Aw.Meta.Tests.Routing.RoutingWorld;

namespace Aw.Meta.Tests.Routing;

public sealed class StandingReadingsTests
{
    private static UsageReading Unknown(string lane) => new(lane, UsageState.Unknown, Stop: null, Idle: false, "no pane");

    private static LaneStanding Codex(string lane, ProviderSignal? signal) =>
        new(lane, "codex", "codex", "gpt-5.5", UsageState.Unknown, Kind: null, ResetsAt: null, signal, Note: null);

    [Fact, Trait("Lane", "unit")]
    public void A_provider_that_reports_its_limit_makes_the_lane_limited_until_the_latest_reset_of_the_full_windows()
    {
        var signal = CodexRateLimits.Read([Rollout(100, 100, primaryResets: Now.AddHours(1), secondaryResets: Now.AddDays(2))], Now);

        var merged = StandingReadings.Merge([Unknown("ui")], [Codex("ui", signal)]);

        var reading = Assert.Single(merged);
        Assert.Equal((UsageState.Limited, UsageKind.Weekly, Now.AddDays(2), false), (reading.State, reading.Stop!.Kind, reading.Stop.ResetsAt, reading.Idle));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_provider_with_room_left_turns_an_unreadable_lane_into_ok_and_leaves_a_limit_stop_alone()
    {
        var signal = CodexRateLimits.Read([Rollout(10, 10)], Now);
        var stopped = new UsageReading("api", UsageState.Limited, new UsageStop(UsageKind.Session, Now.AddHours(1), "x"), Idle: true);

        var merged = StandingReadings.Merge([Unknown("ui"), stopped], [Codex("ui", signal), Codex("api", signal)]);

        Assert.Equal([UsageState.Ok, UsageState.Limited], merged.Select(r => r.State));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_lane_with_no_signal_keeps_its_reading()
    {
        var merged = StandingReadings.Merge([Unknown("ui")], [Codex("ui", signal: null)]);

        Assert.Equal(UsageState.Unknown, Assert.Single(merged).State);
    }
}
