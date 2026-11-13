using Aw.Meta.Core;
using Aw.Meta.Core.Commands;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Features.Watch;
using Aw.Meta.Testing;
using Aw.Meta.Tests.Reserve;

namespace Aw.Meta.Tests.Events;

// The watch writes an event on each edge of a lane's context and session, and never while the state stays the same.
public sealed class WatchEventsTests
{
    private static readonly TimeSpan _interval = TimeSpan.FromSeconds(120);
    private readonly MetaWorld _world = new();
    private readonly InMemoryMetaEventStore _events = new();

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
            new LaneReport(_world.Monitor, _world.Values, new MissingLanes(_world.Sessions, _world.Messages, _world.Lanes), new MetaEventSink(_events)),
            _world.Board);
    }

    private async Task Watch(TimeSpan duration)
    {
        var result = await Service().Handle(new WatchRequest(_interval, duration, _ => ValueTask.CompletedTask), CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
    }

    private async Task<IReadOnlyList<(string Kind, string? Lane, string Fields)>> Written() =>
        [.. (await _events.Read(0, kind: null, lane: null, 100, CancellationToken.None)).Value.Events.Select(e => (e.Kind, e.Lane, e.Fields))];

    private void LaneAtContext(long tokens)
    {
        _world.LaneFiles.Put(new LaneRecord("api", SessionId.From("s-old"), Agent: null, A.At(1)));
        _world.ClaudeTranscripts.PutUsage("s-old", tokens, A.At(5));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Context_crossing_the_recycle_mark_appends_one_context_high()
    {
        await _world.Ready();
        LaneAtContext(487_000);

        await Watch(TimeSpan.FromMinutes(10));

        Assert.Equal([("context.high", "api", """{"lane":"api","tokens_k":487,"recycle":true}""")], await Written());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Staying_above_the_mark_appends_nothing_more_even_in_a_re_armed_watch()
    {
        await _world.Ready();
        LaneAtContext(487_000);
        await Watch(TimeSpan.FromMinutes(10));

        await Watch(TimeSpan.FromMinutes(10));

        _ = Assert.Single(await Written());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Dropping_back_appends_context_ok()
    {
        await _world.Ready();
        LaneAtContext(487_000);
        await Watch(TimeSpan.FromMinutes(10));
        _world.LaneSessions.Set("t1-api", "s-new");
        _world.ClaudeTranscripts.PutUsage("s-new", 9_000, A.At(9));

        await Watch(TimeSpan.FromMinutes(10));

        Assert.Equal(["context.high", "context.ok"], (await Written()).Select(e => e.Kind), StringComparer.Ordinal);
        Assert.Equal("""{"lane":"api","tokens_k":9,"recycle":false}""", (await Written())[1].Fields);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Staying_below_the_mark_appends_nothing()
    {
        await _world.Ready();
        LaneAtContext(40_000);

        await Watch(TimeSpan.FromMinutes(10));

        Assert.Empty(await Written());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_lane_whose_session_is_gone_appends_one_lane_down_across_re_armed_watches()
    {
        await _world.Ready();
        _world.Sessions.Add(A.Live("t1-api", "s-api"));
        _ = await _world.Values.Set(KvKeys.WatchLanes, "up=api,ui;down=", CancellationToken.None);

        await Watch(TimeSpan.FromMinutes(10));
        await Watch(TimeSpan.FromMinutes(10));

        Assert.Equal([("lane.down", "ui", """{"lane":"ui","reason":"no live session"}""")], await Written());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_lane_that_is_back_appends_lane_up()
    {
        await _world.Ready();
        _world.Sessions.Add(A.Live("t1-api", "s-api"));
        _world.Sessions.Add(A.Live("t1-ui", "s-ui"));
        _ = await _world.Values.Set(KvKeys.WatchLanes, "up=api;down=ui", CancellationToken.None);

        await Watch(TimeSpan.FromMinutes(10));

        Assert.Equal([("lane.up", "ui", """{"lane":"ui","reason":"live session"}""")], await Written());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_first_watch_on_a_box_appends_no_lane_event()
    {
        await _world.Ready();
        _world.Sessions.Add(A.Live("t1-api", "s-api"));

        await Watch(TimeSpan.FromMinutes(10));

        Assert.DoesNotContain(await Written(), e => e.Kind.StartsWith("lane.", StringComparison.Ordinal));
    }
}
