using Aw.Context.Core;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Records;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.SyncSources;

/// <summary>
/// The 5-minute index tick asks Linear who the owner is after the projects feed (projects page spec §4.2), so 'source_identity' is filled before the page reads it.
/// </summary>
[Trait("Lane", "unit")]
public sealed class TickViewerTests
{
    private const string Linear = """{"sources":{"linear":{"teams":["SPL"],"token":"env:LINEAR_KEY"}}}""";
    private static readonly SourceId _issues = SourceId.From("linear");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static MapWorld Rig(SourceId feed) => new(feeds: new FakeRecordFeedCatalog(new FakeRecordFeed(feed, [])));

    private static async Task<SourceIdentity?> Stored(MapWorld world) => (await world.Identities.Read(LinearSourceIds.Identity, Ct)).Value;

    [Fact]
    public async Task Tick_on_an_empty_store_fills_source_identity()
    {
        using var world = Rig(LinearSourceIds.Projects);

        var report = await world.Engine.Tick(MapWorld.HumanWith(Linear), Ct);

        Assert.True(report.IsSuccess, report.IsError ? report.Error.Message : null);
        var identity = Assert.IsType<SourceIdentity>(await Stored(world));
        Assert.Equal("linear:user/00000000-0000-0000-0000-0000000000a1", identity.UserRef);
        Assert.Equal("Ada Owner", identity.Name);
        Assert.Equal(world.Clock.GetUtcNow(), identity.ReadAt);
    }

    [Fact]
    public async Task A_second_tick_within_the_hour_makes_no_viewer_call()
    {
        using var world = Rig(LinearSourceIds.Projects);
        var scope = MapWorld.HumanWith(Linear);

        _ = await world.Engine.Tick(scope, Ct);
        world.Clock.Advance(TimeSpan.FromMinutes(59));
        _ = await world.Engine.Tick(scope, Ct);

        Assert.Equal(1, world.Viewers.Calls);
    }

    [Fact]
    public async Task A_tick_after_an_hour_asks_once_more_and_rewrites_the_row()
    {
        using var world = Rig(LinearSourceIds.Projects);
        var scope = MapWorld.HumanWith(Linear);
        _ = await world.Engine.Tick(scope, Ct);
        world.Clock.Advance(TimeSpan.FromMinutes(61));
        world.Viewers.Answer = new SourceViewer(ExternalId.From("00000000-0000-0000-0000-0000000000b2"), "Bea Owner");

        _ = await world.Engine.Tick(scope, Ct);

        Assert.Equal(2, world.Viewers.Calls);
        var identity = Assert.IsType<SourceIdentity>(await Stored(world));
        Assert.Equal("Bea Owner", identity.Name);
        Assert.Equal(world.Clock.GetUtcNow(), identity.ReadAt);
    }

    [Fact]
    public async Task The_step_runs_only_for_the_projects_feed()
    {
        using var world = Rig(_issues);

        _ = await world.Engine.Tick(MapWorld.HumanWith(Linear), Ct);

        Assert.Equal(0, world.Viewers.Calls);
        Assert.Null(await Stored(world));
    }

    [Fact]
    public async Task The_step_does_not_run_when_linear_is_not_configured()
    {
        using var world = Rig(LinearSourceIds.Projects);

        _ = await world.Engine.Tick(MapWorld.HumanWith("""{"sources":{"linear":{"teams":["SPL"]}}}"""), Ct);

        Assert.Equal(0, world.Viewers.Calls);
        Assert.Null(await Stored(world));
    }

    [Fact]
    public async Task A_failed_viewer_call_leaves_the_row_and_the_feeds_result_ok()
    {
        using var world = Rig(LinearSourceIds.Projects);
        var scope = MapWorld.HumanWith(Linear);
        _ = await world.Engine.Tick(scope, Ct);
        world.Clock.Advance(TimeSpan.FromMinutes(61));
        var before = await Stored(world);
        world.Viewers.Answer = ContextErrors.LinearKeyRefused.With("revoked");

        var report = await world.Engine.Tick(scope, Ct);

        Assert.True(report.IsSuccess, report.IsError ? report.Error.Message : null);
        var row = Assert.Single(report.Value.Sources);
        Assert.Null(row.Code);
        Assert.Equal(before, await Stored(world));
    }
}
