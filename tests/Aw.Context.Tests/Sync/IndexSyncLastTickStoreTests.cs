using Aw.Context.Core;
using Aw.Context.Core.Records;
using Aw.Context.Testing.Engine;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Context.Features.SyncSources;
using Aw.Context.Infrastructure.Records;
using Aw.Context.Infrastructure.Store;
using Aw.Context.Testing.Records;
using Aw.Context.Tests.Infrastructure;
using Aw.Kernel;
using MapWorld = Aw.Context.Tests.Map.MapWorld;

namespace Aw.Context.Tests.Sync;

/// <summary>`sindri doctor` reads the index progress in its own process: the last tick's end and code have to come from the record store, not from the serving process's memory (#1591).</summary>
[Trait("Lane", "integration")]
public sealed class IndexSyncLastTickStoreTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static RecordItem Item() => new(
        RecordKey.From("linear:item/1"), RecordKind.Issue, "Title 1", "Words of linear item 1", Author: null, Url: null, RecordState.Open, StateRaw: null,
        CreatedAt: null, RecordSamples.Now.AddDays(-1), ClosedAt: null, SourceVersion.From("v1"), Links: [], Repo: null, Audience: AudienceKey.From("linear:team-a"));

    private static IndexSync Instance(TempHome home, RecordFeed feed)
    {
        var store = new SqliteRecordStore(new ContextDatabase(home.Settings, home.Clock));
        return new IndexSync(new RecordFeedSync(new FakeRecordFeedCatalog(feed), store, home.Clock), store, new FakeScrubberFactory());
    }

    [Fact]
    public async Task Progress_reads_the_last_tick_from_the_store_in_a_fresh_process()
    {
        using var home = new TempHome();
        var feed = new FakeRecordFeed(RecordSamples.Linear, [Item()]);
        var ticked = await Instance(home, feed).Tick(MapWorld.Human, Ct);
        Assert.True(ticked.IsSuccess, ticked.IsError ? ticked.Error.Message : null);
        var tickEnd = home.Clock.GetUtcNow();

        var progress = await Instance(home, feed).Progress(MapWorld.Human, Ct);

        Assert.True(progress.IsSuccess, progress.IsError ? progress.Error.Message : null);
        var row = Assert.Single(progress.Value);
        Assert.Equal(tickEnd, row.LastSync);
        Assert.Null(row.LastCode);
    }

    [Fact]
    public async Task Progress_reads_a_failed_ticks_code_from_the_store_in_a_fresh_process()
    {
        using var home = new TempHome();
        var feed = new DownFeed(RecordSamples.Linear);
        _ = await Instance(home, feed).Tick(MapWorld.Human, Ct);
        var tickEnd = home.Clock.GetUtcNow();

        var progress = await Instance(home, feed).Progress(MapWorld.Human, Ct);

        Assert.True(progress.IsSuccess, progress.IsError ? progress.Error.Message : null);
        var row = Assert.Single(progress.Value);
        Assert.Equal(tickEnd, row.LastSync);
        Assert.Equal(ContextErrors.SourceUnavailable.Code, row.LastCode);
    }

    /// <summary>A feed whose source is down: every pull fails with SND-CONTEXT-010.</summary>
    private sealed class DownFeed(SourceId source) : RecordFeed(source)
    {
        public override ValueTask<Result<RecordPage>> Pull(RecordPull pull, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<RecordPage>>(ContextErrors.SourceUnavailable.With("down"));
    }
}
