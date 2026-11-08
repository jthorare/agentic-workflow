using Aw.Meta.Core.Config;
using Aw.Meta.Tests.Usage;

namespace Aw.Meta.Tests.Routing;

public sealed class RouteWatcherTests
{
    private readonly RoutingWorld _w = new();

    private async Task<IReadOnlyList<string>> Check()
    {
        var team = (await _w.U.World.Config.Load(CancellationToken.None)).Value;
        var readings = (await _w.U.Reader.Read(team, CancellationToken.None)).Value;
        return await _w.U.Routes.Check(team, readings, CancellationToken.None);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_suggestion_is_printed_once()
    {
        await _w.Ready();
        _w.U.LimitLane("ui");

        var first = await Check();
        var second = await Check();

        Assert.Equal(["ROUTE SUGGEST ui claude-code/opus -> codex/astra: limited: session limit, resets 2026-10-15 05:30:00Z; codex usage unknown"], first, StringComparer.Ordinal);
        Assert.Empty(second);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_suggestion_that_went_away_is_printed_again_when_it_comes_back()
    {
        await _w.Ready();
        _w.U.LimitLane("ui");
        _ = await Check();
        _w.U.World.Panes.Show("t1-ui", UsageWorld.Idle("⏺ fine"));
        Assert.Empty(await Check());

        _w.U.LimitLane("ui");

        _ = Assert.Single(await Check());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_different_target_is_a_new_suggestion()
    {
        await _w.Ready();
        _w.U.LimitLane("ui");
        _ = await Check();
        var team = (await _w.U.World.Config.Load(CancellationToken.None)).Value;
        _ = await _w.U.World.Config.Save(team with { Routing = team.Routing with { DefaultModel = "haiku" } }, CancellationToken.None);

        var lines = await Check();

        Assert.StartsWith("ROUTE SUGGEST ui claude-code/haiku -> codex/luna", Assert.Single(lines), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_team_without_pairs_prints_and_keeps_nothing()
    {
        await _w.Ready(new RoutingConfig());
        _w.U.LimitLane("ui");

        Assert.Empty(await Check());
        Assert.Null((await _w.U.World.Values.Get(Core.KvKeys.RouteWatch, CancellationToken.None)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_watch_prints_the_route_line_after_the_usage_lines()
    {
        await _w.Ready();
        _w.U.LimitLane("ui");

        var lines = await _w.U.Monitor.Check((await _w.U.World.Config.Load(CancellationToken.None)).Value, CancellationToken.None);

        Assert.StartsWith("USAGE LIMITED ui", lines[0], StringComparison.Ordinal);
        Assert.StartsWith("ROUTE SUGGEST ui", lines[^1], StringComparison.Ordinal);
    }
}
