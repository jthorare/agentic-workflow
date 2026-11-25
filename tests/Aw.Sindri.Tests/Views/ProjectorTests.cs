using System.Globalization;
using Aw.Kernel.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Host.Server;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Server;

namespace Aw.Sindri.Tests.Views;

/// <summary>The projector over the in-memory ledger and views (API spec §2.3), driven pass by pass on the fake clock.</summary>
public sealed class ProjectorTests
{
    private readonly SliceHarness _h = new();
    private readonly InMemoryViewStore _views = new(SliceHarness.Json);
    private readonly ListLogger<Projector> _log = new();
    private readonly ToggleProjection _projection = new();
    private readonly ProjectorProgress _progress;
    private readonly CountingLedgerReader _reader;

    public ProjectorTests()
    {
        _progress = new ProjectorProgress(_h.Clock);
        _reader = new CountingLedgerReader(_h.Store);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task Projector_folds_pages_of_100_until_it_reaches_the_head()
    {
        await Append(250);

        var pass = await Projector().CatchUp(Token);

        Assert.Equal(ProjectorPass.CaughtUp, pass);
        Assert.Equal([100, 100, 100], _reader.Pages);
        Assert.True(_progress.Current is { Cursor: 250, LedgerHead: 250, Stalled: false, Rebuilding: false });
        Assert.Equal(250, (await _views.List<ServiceRow>(ViewNames.Services, Token)).Value.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Projector_folds_pages_of_1000_while_more_than_1000_events_behind_then_pages_of_100()
    {
        await Append(2300);

        var pass = await Projector().CatchUp(Token);

        Assert.Equal(ProjectorPass.CaughtUp, pass);
        Assert.Equal([1000, 1000, 100, 100, 100], _reader.Pages);
        Assert.True(_progress.Current is { Cursor: 2300, LedgerHead: 2300, Stalled: false });
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_throwing_projection_stalls_at_that_sequence_and_logs_SND_VIEW_001()
    {
        var projector = Projector();
        await Append(1);
        _ = await projector.CatchUp(Token);
        _projection.Throw = true;
        await Append(2);

        var pass = await projector.CatchUp(Token);

        Assert.Equal(ProjectorPass.Stalled, pass);
        Assert.True(_progress.Current is { Cursor: 1, LedgerHead: 3, Stalled: true, StalledAt: 2 });
        Assert.Equal(ToggleProjection.Put, _progress.Current.StalledType);
        Assert.Contains(_log.Lines, l => l.Contains("SND-VIEW-001", StringComparison.Ordinal) && l.Contains(" 2 ", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_stalled_projector_retries_every_30_seconds()
    {
        var projector = Projector();
        _projection.Fail = true;
        await Append(2);
        Assert.Equal(ProjectorPass.Stalled, await projector.CatchUp(Token));
        var calls = _projection.Calls;
        _projection.Fail = false;

        _h.Clock.Advance(TimeSpan.FromSeconds(29));
        Assert.Equal(ProjectorPass.Stalled, await projector.CatchUp(Token));
        Assert.Equal(calls, _projection.Calls);   // no retry before 30 s
        _h.Clock.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(ProjectorPass.CaughtUp, await projector.CatchUp(Token));
        Assert.True(_progress.Current is { Cursor: 2, Stalled: false });
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_rebuild_resets_the_cursor_and_replays()
    {
        var projector = Projector();
        await Append(3);
        _ = await projector.CatchUp(Token);
        var calls = _projection.Calls;

        _progress.RequestRebuild();
        Assert.True(_progress.Current.Rebuilding);
        var pass = await projector.CatchUp(Token);

        Assert.Equal(ProjectorPass.CaughtUp, pass);
        Assert.Equal(calls + 3, _projection.Calls);
        Assert.True(_progress.Current is { Cursor: 3, Rebuilding: false });
        Assert.True((await _views.State(Token)).Value is { Cursor: 3, Rebuilding: false });
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_schema_version_change_rebuilds()
    {
        await Append(3);
        _ = await _views.Open(ViewSchema.Version - 1, Token);   // folded by an older build

        _ = await Projector().CatchUp(Token);

        Assert.Equal(3, _projection.Calls);
        Assert.True((await _views.State(Token)).Value is { Cursor: 3, SchemaVersion: ViewSchema.Version, Rebuilding: false });
        Assert.Contains(_log.Lines, l => l.Contains("schema", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Rebuild_clears_a_stall()
    {
        var projector = Projector();
        _projection.Fail = true;
        await Append(2);
        Assert.Equal(ProjectorPass.Stalled, await projector.CatchUp(Token));
        _projection.Fail = false;

        _progress.RequestRebuild();   // no 30 s wait: a rebuild runs at once

        Assert.Equal(ProjectorPass.CaughtUp, await projector.CatchUp(Token));
        Assert.True(_progress.Current is { Cursor: 2, Stalled: false, StalledAt: null, Rebuilding: false });
    }

    private Projector Projector() => new(_reader, _views, [_projection], _progress, _h.Callers, _h.Clock, _log);

    private async Task Append(int count)
    {
        var grant = (await _h.Store.Read(Token)).Value is null ? await _h.HoldLease() : _h.Held.Current!;   // why: Read found the held lease
        for (var i = 0; i < count; i++)
        {
            var key = string.Create(CultureInfo.InvariantCulture, $"k{_h.Store.Events.Count + 1:D4}");
            var context = EventContext.Create("host:sindri", "local", 0, _h.Ids.NewId()).Value;
            _ = await _h.Store.Append(new LedgerEntry(context.Envelope(ToggleProjection.Put, """{"n":1}""", _h.Clock, _h.Ids), key, Item: null), grant, Token);
        }
    }
}
