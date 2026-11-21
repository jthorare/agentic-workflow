using Aw.Meta.Core.Config;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Usage;
using Aw.Meta.Testing;
using Aw.Meta.Tests.Usage;

namespace Aw.Meta.Tests.Events;

public sealed class UsageEventsTests
{
    private readonly UsageWorld _w = new();
    private readonly InMemoryMetaEventStore _events = new();

    private async Task Tick()
    {
        var team = (await _w.World.Config.Load(CancellationToken.None)).Value with { Usage = new UsageConfig() };
        var readings = await _w.Reader.Read(team, CancellationToken.None);
        var watcher = new UsageWatcher(new PaneSubmitter(_w.World.Panes, _w.World.Delay), _w.World.Log, _w.World.Values, _w.World.Clock, new MetaEventSink(_events));
        _ = await watcher.Tick(team, readings.Value, CancellationToken.None);
    }

    private async Task<IReadOnlyList<(string Kind, string? Lane, string Fields)>> Written() =>
        [.. (await _events.Read(0, kind: null, lane: null, 100, CancellationToken.None)).Value.Events.Select(e => (e.Kind, e.Lane, e.Fields))];

    [Fact, Trait("Lane", "unit")]
    public async Task A_new_stop_appends_one_usage_limited_with_the_reset_time()
    {
        await _w.Ready();
        _w.LimitLane("api");

        await Tick();
        await Tick();

        Assert.Equal([("usage.limited", "api", """{"lane":"api","provider":"claude","until":"2026-10-15T05:30:00Z"}""")], await Written());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_window_coming_back_appends_one_usage_ok_and_the_probe_repeats_append_none()
    {
        await _w.Ready();
        _w.LimitLane("api");
        await Tick();
        _w.World.Clock.Advance(TimeSpan.FromMinutes(40));

        await Tick();
        _w.World.Clock.Advance(TimeSpan.FromMinutes(40));
        await Tick();

        Assert.Equal(["usage.limited", "usage.ok"], (await Written()).Select(e => e.Kind), StringComparer.Ordinal);
        Assert.Equal("""{"lane":"api","provider":"claude","until":"2026-10-15T05:30:00Z"}""", (await Written())[1].Fields);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_lane_that_is_not_limited_appends_nothing()
    {
        await _w.Ready();

        await Tick();

        Assert.Empty(await Written());
    }
}
