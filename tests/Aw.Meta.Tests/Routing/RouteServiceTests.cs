using Aw.Meta.Core.Config;
using Aw.Meta.Features.Routing;

namespace Aw.Meta.Tests.Routing;

public sealed class RouteServiceTests
{
    private readonly RoutingWorld _w = new();

    [Fact, Trait("Lane", "unit")]
    public async Task A_limited_lane_gets_a_suggestion_and_the_config_is_left_alone()
    {
        await _w.Ready();
        _w.U.LimitLane("ui");

        var result = await _w.Service().Handle(new RouteRequest(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var move = Assert.Single(result.Value.Suggestions);
        Assert.Equal(("ui", "codex", "gpt-5.5"), (move.Lane, move.ToAgent, move.ToModel));
        Assert.True(result.Value.Attention);
        Assert.False(result.Value.Applied);
        Assert.Empty((await _w.U.World.Config.Load(CancellationToken.None)).Value.Routing.Lanes);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Apply_writes_the_lane_entry_and_the_next_call_waits_for_the_relaunch()
    {
        await _w.Ready();
        _w.U.LimitLane("ui");

        var applied = await _w.Service().Handle(new RouteRequest(Apply: true), CancellationToken.None);

        Assert.True(applied.Value.Applied);
        Assert.False(applied.Value.Attention);
        Assert.Equal(new LaneRoute("codex", "gpt-5.5"), (await _w.U.World.Config.Load(CancellationToken.None)).Value.Routing.Lanes["ui"]);

        var again = await _w.Service().Handle(new RouteRequest(), CancellationToken.None);

        Assert.Empty(again.Value.Suggestions);
        Assert.Contains("already assigned to codex", Assert.Single(again.Value.Skipped).Reason, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Apply_keeps_the_lane_entries_it_does_not_touch()
    {
        await _w.Ready(RoutingWorld.Cfg() with { Lanes = new Dictionary<string, LaneRoute>(StringComparer.Ordinal) { ["api"] = new(Model: "haiku") } });
        _w.U.LimitLane("ui");

        _ = await _w.Service().Handle(new RouteRequest(Apply: true), CancellationToken.None);

        var lanes = (await _w.U.World.Config.Load(CancellationToken.None)).Value.Routing.Lanes;
        Assert.Equal(["api", "ui"], lanes.Keys.Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_lane_filter_limits_the_suggestions_and_the_write_to_that_lane()
    {
        await _w.Ready();
        _w.U.LimitLane("ui");
        _w.U.LimitLane("api");

        var result = await _w.Service().Handle(new RouteRequest(Apply: true, Lane: "api"), CancellationToken.None);

        Assert.Equal(["api"], result.Value.Suggestions.Select(s => s.Lane), StringComparer.Ordinal);
        Assert.Equal(["api"], (await _w.U.World.Config.Load(CancellationToken.None)).Value.Routing.Lanes.Keys, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_lane_filter_that_names_no_lane_fails()
    {
        await _w.Ready();

        var result = await _w.Service().Handle(new RouteRequest(Lane: "nope"), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Contains("nope", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_team_without_pairs_gets_nothing_even_when_a_lane_is_limited()
    {
        await _w.Ready(new RoutingConfig());
        _w.U.LimitLane("ui");

        var result = await _w.Service().Handle(new RouteRequest(), CancellationToken.None);

        Assert.Empty(result.Value.Suggestions);
        Assert.False(result.Value.Attention);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_a_team_config_the_call_fails()
    {
        var result = await _w.Service().Handle(new RouteRequest(), CancellationToken.None);

        Assert.True(result.IsError);
    }
}
