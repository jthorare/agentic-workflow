using Aw.Meta.Core.Commands;
using Aw.Meta.Core.Usage;
using Aw.Meta.Features.Watch;
using Aw.Meta.Tests.Reserve;

namespace Aw.Meta.Tests.Usage;

public sealed class WatchUsageTests
{
    private static readonly TimeSpan _interval = TimeSpan.FromSeconds(120);
    private readonly UsageWorld _u = new();
    private readonly List<string> _lines = [];

    private WatchService Service()
    {
        var w = _u.World;
        _ = w.Runner
            .When("gh pr list", _ => new CommandResult(0, "[]", string.Empty, TimedOut: false))
            .When("git ls-remote origin main", _ => new CommandResult(0, "aaaaaaa1111111\trefs/heads/main\n", string.Empty, TimedOut: false));
        return new WatchService(
            w.Config,
            new WatchPoller(w.Repo, w.Resources, w.InboxCounts, w.Values, new WatchSweeper(w.Config, w.Sweeper, w.Values), Worlds.Reservation(w), w.Context),
            w.Values,
            w.Delay,
            w.Clock,
            _u.Monitor);
    }

    private async Task Watch(TimeSpan duration)
    {
        var result = await Service().Handle(new WatchRequest(_interval, duration, line => { _lines.Add(line); return ValueTask.CompletedTask; }), CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
    }

    private IEnumerable<string> Usage() => _lines.Where(l => l.StartsWith("USAGE", StringComparison.Ordinal));

    [Fact, Trait("Lane", "unit")]
    public async Task A_limit_is_announced_once_and_when_the_window_is_back_the_lane_is_told_to_go_on()
    {
        await _u.Ready();
        _u.LimitLane("api");

        await Watch(TimeSpan.FromMinutes(45));

        Assert.Equal(["USAGE LIMITED api session resets 2026-10-15 05:30:00Z", "USAGE OK api"], Usage(), StringComparer.Ordinal);
        Assert.Equal([("t1-api", UsageWatch.ResumeLine)], _u.World.Panes.Woken);
        Assert.Equal(2, _u.World.Log.Lines.Count(l => l.Contains("api", StringComparison.Ordinal)));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_re_armed_watch_does_not_announce_a_standing_limit_again()
    {
        await _u.Ready();
        _u.LimitLane("api");

        await Watch(TimeSpan.FromMinutes(10));
        await Watch(TimeSpan.FromMinutes(10));

        Assert.Equal(["USAGE LIMITED api session resets 2026-10-15 05:30:00Z"], Usage(), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_meta_limit_prints_the_wake_lines_the_meta_sees_from_its_background_watch()
    {
        await _u.Ready();
        _u.World.ClaudeTranscripts.Put(Testing.MetaWorld.MetaSessionId, UsageWorld.LimitLine + "\n", Testing.A.At(58));

        await Watch(TimeSpan.FromMinutes(45));

        Assert.Equal(
            [
                "USAGE LIMITED meta session resets 2026-10-15 05:30:00Z",
                "USAGE OK meta",
                "USAGE OK meta: resume your subagents (SendMessage them) and re-arm the watch",
            ],
            Usage(),
            StringComparer.Ordinal);
        Assert.Empty(_u.World.Panes.Woken);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_team_that_is_not_limited_adds_no_usage_lines()
    {
        await _u.Ready();

        await Watch(TimeSpan.FromMinutes(10));

        Assert.Empty(Usage());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_Codex_lane_at_its_weekly_limit_is_announced_once_and_back_when_the_window_resets_and_is_never_typed_into()
    {
        await _u.Ready();
        _u.World.LaneFiles.Put(new Core.Lanes.LaneRecord("ui", Core.Ids.SessionId.From("codex-ui"), new Core.Lanes.LaneAgent("codex", "codex", Hooks: true), Testing.A.At(1)));
        _u.World.CodexRollouts.Put("codex-ui", Routing.RoutingWorld.Rollout(10, 100) + "\n", Testing.A.At(55));

        await Watch(TimeSpan.FromMinutes(20));
        await Watch(TimeSpan.FromMinutes(20));

        Assert.Equal(["USAGE LIMITED ui weekly resets 2026-10-18 05:00:00Z"], Usage(), StringComparer.Ordinal);

        _u.World.CodexRollouts.Put("codex-ui", Routing.RoutingWorld.Rollout(10, 12) + "\n", Testing.A.At(120));
        await Watch(TimeSpan.FromMinutes(5));

        Assert.Equal(["USAGE LIMITED ui weekly resets 2026-10-18 05:00:00Z", "USAGE OK ui"], Usage(), StringComparer.Ordinal);
        Assert.Empty(_u.World.Panes.Woken);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_Codex_lane_at_its_5h_limit_reads_as_a_session_limit()
    {
        await _u.Ready();
        _u.World.LaneFiles.Put(new Core.Lanes.LaneRecord("ui", Core.Ids.SessionId.From("codex-ui"), new Core.Lanes.LaneAgent("codex", "codex", Hooks: true), Testing.A.At(1)));
        _u.World.CodexRollouts.Put("codex-ui", Routing.RoutingWorld.Rollout(100, 30) + "\n", Testing.A.At(55));

        await Watch(TimeSpan.FromMinutes(10));

        Assert.Equal(["USAGE LIMITED ui session resets 2026-10-15 07:00:00Z"], Usage(), StringComparer.Ordinal);
    }
}
