using Aw.Meta.Core.Routing;
using static Aw.Meta.Tests.Routing.RoutingWorld;

namespace Aw.Meta.Tests.Routing;

public sealed class RoutePlannerTests
{
    [Fact, Trait("Lane", "unit")]
    public void A_limited_Claude_lane_moves_to_the_Codex_model_of_equal_performance()
    {
        var plan = RoutePlanner.Plan(Cfg(), [Standing("ui", "claude-code", "opus", limited: true)]);

        var move = Assert.Single(plan.Suggestions);
        Assert.Equal(("ui", "claude-code", "opus", "codex", "astra"), (move.Lane, move.FromAgent, move.FromModel, move.ToAgent, move.ToModel));
        Assert.Equal("limited: session limit, resets 2026-10-15 05:30:00Z", move.Reason);
        Assert.Equal("switch ui from claude-code/opus to codex/astra because limited: session limit, resets 2026-10-15 05:30:00Z", move.ToString());
    }

    [Fact, Trait("Lane", "unit")]
    public void A_lane_with_room_left_stays()
    {
        var plan = RoutePlanner.Plan(Cfg(), [Standing("api", "claude-code", "opus"), Standing("ui", "codex", "astra", usedPct: 80)]);

        Assert.Empty(plan.Suggestions);
        Assert.Empty(plan.Skipped);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_Codex_lane_under_the_remaining_share_moves_back_through_the_pair_read_the_other_way()
    {
        var plan = RoutePlanner.Plan(Cfg(), [Standing("ui", "codex", "astra", usedPct: 95)]);

        var move = Assert.Single(plan.Suggestions);
        Assert.Equal(("codex", "astra", "claude-code", "opus"), (move.FromAgent, move.FromModel, move.ToAgent, move.ToModel));
        Assert.Equal("only 5% left in the fullest window (under 10%)", move.Reason);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_remaining_share_must_be_under_the_threshold_to_move_and_zero_turns_the_check_off()
    {
        Assert.Empty(RoutePlanner.Plan(Cfg(), [Standing("ui", "codex", "astra", usedPct: 90)]).Suggestions);
        _ = Assert.Single(RoutePlanner.Plan(Cfg(), [Standing("ui", "codex", "astra", usedPct: 90.5)]).Suggestions);
        Assert.Empty(RoutePlanner.Plan(Cfg(minRemainingPct: 0), [Standing("ui", "codex", "astra", usedPct: 99)]).Suggestions);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_provider_that_shows_no_share_never_triggers_the_remaining_check()
    {
        var plan = RoutePlanner.Plan(Cfg(), [Standing("ui", "claude-code", "opus")]);

        Assert.Empty(plan.Suggestions);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_limit_the_provider_reports_moves_the_lane_even_without_a_limit_stop()
    {
        var plan = RoutePlanner.Plan(Cfg(), [Standing("ui", "codex", "astra", usedPct: 40, reached: true)]);

        Assert.Equal("limited: codex reports its limit reached", Assert.Single(plan.Suggestions).Reason);
    }

    [Fact, Trait("Lane", "unit")]
    public void With_switch_when_limited_off_a_limit_alone_moves_nothing()
    {
        var plan = RoutePlanner.Plan(Cfg(switchWhenLimited: false), [Standing("ui", "claude-code", "opus", limited: true)]);

        Assert.Empty(plan.Suggestions);
    }

    [Fact, Trait("Lane", "unit")]
    public void No_move_when_the_target_provider_is_running_out_too()
    {
        var plan = RoutePlanner.Plan(Cfg(), [Standing("ui", "claude-code", "opus", limited: true), Standing("api", "codex", "astra", limited: true)]);

        Assert.DoesNotContain(plan.Suggestions, s => s.Lane == "ui");
        Assert.Contains(plan.Skipped, s => s.Lane == "ui" && s.Reason.Contains("codex is running out too (lane api)", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_move_to_a_provider_with_no_reading_says_its_usage_is_unknown()
    {
        var plan = RoutePlanner.Plan(Cfg(), [Standing("ui", "claude-code", "opus", limited: true)]);

        Assert.EndsWith("; codex usage unknown", Assert.Single(plan.Suggestions).Reason, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_move_to_a_provider_with_a_healthy_reading_has_no_such_note()
    {
        var plan = RoutePlanner.Plan(Cfg(), [Standing("ui", "claude-code", "opus", limited: true), Standing("api", "codex", "luna", usedPct: 20)]);

        Assert.DoesNotContain("unknown", Assert.Single(plan.Suggestions).Reason, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_model_with_no_equivalent_is_skipped_and_the_reason_names_the_key_to_set()
    {
        var plan = RoutePlanner.Plan(Cfg(), [Standing("ui", "claude-code", "sonnet", limited: true)]);

        Assert.Empty(plan.Suggestions);
        Assert.Equal("ui", Assert.Single(plan.Skipped).Lane);
        Assert.Contains("no equivalent for sonnet in routing.equivalents", plan.Skipped[0].Reason, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_lane_whose_model_is_unknown_is_skipped_and_the_reason_names_the_keys_to_set()
    {
        var plan = RoutePlanner.Plan(Cfg(defaultModel: null), [Standing("ui", "claude-code", model: null, limited: true)]);

        Assert.Empty(plan.Suggestions);
        Assert.Contains("routing.lanes.ui.model or routing.defaultModel", plan.Skipped[0].Reason, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_lane_already_switched_in_the_config_waits_for_its_relaunch()
    {
        var plan = RoutePlanner.Plan(Cfg(), [Standing("ui", "codex", "astra", limited: true, runningAgent: "claude-code")]);

        Assert.Empty(plan.Suggestions);
        Assert.Contains("already assigned to codex; relaunch the lane", plan.Skipped[0].Reason, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_equivalent_that_no_provider_lists_is_skipped()
    {
        var routing = Cfg() with { Equivalents = new Dictionary<string, string>(StringComparer.Ordinal) { ["opus"] = "ghost" } };

        var plan = RoutePlanner.Plan(routing, [Standing("ui", "claude-code", "opus", limited: true)]);

        Assert.Contains("ghost is not listed under routing.models", plan.Skipped[0].Reason, StringComparison.Ordinal);
    }
}
