using Aw.Meta.Core;
using Aw.Meta.Core.Commands;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Features.Watch;
using Aw.Meta.Testing;
using Aw.Meta.Tests.Reserve;

namespace Aw.Meta.Tests.Watch;

// #2856: an OOM kill stopped a lane's whole tmux pane and nothing told the meta.
public sealed class WatchLaneSessionsTests
{
    private static readonly TimeSpan _interval = TimeSpan.FromSeconds(120);
    private readonly MetaWorld _world = new();
    private readonly List<string> _lines = [];

    private WatchService Service()
    {
        _ = _world.Runner
            .When("gh pr list", _ => new CommandResult(0, "[]", string.Empty, TimedOut: false))
            .When("git ls-remote origin main", _ => new CommandResult(0, "aaaaaaa1111111\trefs/heads/main\n", string.Empty, TimedOut: false));
        return new WatchService(
            _world.Config,
            new WatchPoller(_world.Repo, _world.Resources, _world.InboxCounts, _world.Values, Worlds.FollowUps(_world), Worlds.Reservation(_world), _world.Context),
            _world.Values,
            _world.Delay,
            _world.Clock,
            new LaneReport(_world.Monitor, _world.Values, new MissingLanes(_world.Sessions, _world.Messages, _world.Lanes)),
            _world.Board);
    }

    private async Task Watch(TimeSpan duration)
    {
        var result = await Service().Handle(new WatchRequest(_interval, duration, line => { _lines.Add(line); return ValueTask.CompletedTask; }), CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
    }

    private IEnumerable<string> LaneLines() => _lines.Where(l => l.StartsWith("LANE", StringComparison.Ordinal));

    private async Task<string?> Kept() => (await _world.Values.Get(KvKeys.WatchLanes, CancellationToken.None)).Value;

    [Fact, Trait("Lane", "unit")]
    public async Task A_lane_whose_session_is_gone_is_reported_down_once_across_re_armed_watches()
    {
        await _world.Ready();
        _world.Sessions.Add(A.Live("t1-api", "s-api"));
        _ = await _world.Values.Set(KvKeys.WatchLanes, "up=api,ui;down=", CancellationToken.None);

        await Watch(TimeSpan.FromMinutes(10));
        await Watch(TimeSpan.FromMinutes(10));

        Assert.Equal(["LANE DOWN ui: no live session; restart it with aw-meta-launch --lanes ui"], LaneLines(), StringComparer.Ordinal);
        Assert.Equal("up=api;down=ui", await Kept());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_lane_reported_down_that_is_back_is_reported_up()
    {
        await _world.Ready();
        _world.Sessions.Add(A.Live("t1-api", "s-api"));
        _world.Sessions.Add(A.Live("t1-ui", "s-ui"));
        _ = await _world.Values.Set(KvKeys.WatchLanes, "up=api;down=ui", CancellationToken.None);

        await Watch(TimeSpan.FromMinutes(10));

        Assert.Equal(["LANE UP ui"], LaneLines(), StringComparer.Ordinal);
        Assert.Equal("up=api,ui;down=", await Kept());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_first_watch_on_a_box_records_the_lanes_and_reports_none()
    {
        await _world.Ready();
        _world.Sessions.Add(A.Live("t1-api", "s-api"));

        await Watch(TimeSpan.FromMinutes(10));

        Assert.Empty(LaneLines());
        Assert.Equal("up=api;down=", await Kept());
    }
}
