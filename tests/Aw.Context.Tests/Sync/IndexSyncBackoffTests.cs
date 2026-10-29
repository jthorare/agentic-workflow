using Aw.Context.Core;
using Aw.Context.Core.Records;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Context.Features.SyncSources;
using Aw.Context.Infrastructure.Records;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Records;
using Aw.Context.Tests.Infrastructure;
using Aw.Kernel;
using Microsoft.Extensions.Time.Testing;
using MapWorld = Aw.Context.Tests.Map.MapWorld;

namespace Aw.Context.Tests.Sync;

/// <summary>A failing source backs off between scheduled ticks and the owner's named sync overrides it; the backoff survives a restart (#1597 part A, piece 4).</summary>
public sealed class IndexSyncBackoffTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static IndexSync Rig(IRecordStore store, FakeTimeProvider clock, RecordFeed feed, out RecordFeedSync feeds)
    {
        feeds = new RecordFeedSync(new FakeRecordFeedCatalog(feed), store, clock);
        return new IndexSync(feeds, store, new FakeScrubberFactory());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failed_tick_sets_the_backoff_and_a_scheduled_tick_inside_it_does_not_pull()
    {
        var store = new FakeRecordStore();
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 29, 12, 0, 0, TimeSpan.Zero));
        var feed = new SwitchFeed(RecordSamples.Linear) { Down = true };
        var index = Rig(store, clock, feed, out _);

        _ = await index.Tick(MapWorld.Human, Ct);
        clock.Advance(TimeSpan.FromMinutes(4));
        var skipped = await index.Tick(MapWorld.Human, Ct);

        Assert.Equal(1, feed.Pulls);
        Assert.Equal(ContextErrors.SourceUnavailable.Code, Assert.Single(skipped.Value.Sources).Code);
        var backoff = (await store.Backoff(RecordSamples.Linear, Ct)).Value;
        Assert.Equal((1, clock.GetUtcNow().AddMinutes(1)), (backoff.Failures, backoff.NextTry));
        Assert.False(string.IsNullOrWhiteSpace(backoff.LastErrorText));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_scheduled_tick_pulls_again_once_the_next_try_has_passed_and_a_success_clears_the_backoff()
    {
        var store = new FakeRecordStore();
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 29, 12, 0, 0, TimeSpan.Zero));
        var feed = new SwitchFeed(RecordSamples.Linear) { Down = true };
        var index = Rig(store, clock, feed, out _);
        _ = await index.Tick(MapWorld.Human, Ct);

        clock.Advance(TimeSpan.FromMinutes(5));
        feed.Down = false;
        _ = await index.Tick(MapWorld.Human, Ct);

        Assert.Equal(2, feed.Pulls);
        Assert.Equal(new SourceBackoffState(Failures: 0, NextTry: null, LastCommit: clock.GetUtcNow(), LastErrorText: null), (await store.Backoff(RecordSamples.Linear, Ct)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_named_sync_of_a_source_in_backoff_pulls_it_anyway()
    {
        var store = new FakeRecordStore();
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 29, 12, 0, 0, TimeSpan.Zero));
        var feed = new SwitchFeed(RecordSamples.Linear) { Down = true };
        var index = Rig(store, clock, feed, out _);
        _ = await index.Tick(MapWorld.Human, Ct);

        var named = await index.Tick(MapWorld.Human, RecordSamples.Linear, Ct);

        Assert.Equal(2, feed.Pulls);
        Assert.Equal(2, (await store.Backoff(RecordSamples.Linear, Ct)).Value.Failures);
        Assert.Equal(ContextErrors.SourceUnavailable.Code, Assert.Single(named.Value.Sources).Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_tick_that_hit_its_cap_is_not_a_failure()
    {
        var store = new FakeRecordStore();
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 29, 12, 0, 0, TimeSpan.Zero));
        var feed = new SwitchFeed(RecordSamples.Linear) { Down = true };
        var index = Rig(store, clock, feed, out var feeds);
        _ = await index.Tick(MapWorld.Human, Ct);

        await feeds.Note(RecordSamples.Linear, ContextErrors.SyncCapped.Code, Ct);

        Assert.Equal(new SourceBackoffState(Failures: 0, NextTry: null, LastCommit: clock.GetUtcNow(), LastErrorText: null), (await store.Backoff(RecordSamples.Linear, Ct)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Progress_reports_the_pull_word_the_counts_and_the_last_error()
    {
        var store = new FakeRecordStore();
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 29, 12, 0, 0, TimeSpan.Zero));
        var feed = new SwitchFeed(RecordSamples.Linear) { Down = true };
        var index = Rig(store, clock, feed, out var feeds);
        var idle = Assert.Single((await index.Progress(MapWorld.Human, Ct)).Value);
        _ = await index.Tick(MapWorld.Human, Ct);

        var backoff = Assert.Single((await index.Progress(MapWorld.Human, Ct)).Value);
        clock.Advance(TimeSpan.FromMinutes(5));
        var due = Assert.Single((await index.Progress(MapWorld.Human, Ct)).Value);
        SourceProgress? running = null;
        _ = await feeds.Locked(RecordSamples.Linear, async () =>
        {
            running = Assert.Single((await index.Progress(MapWorld.Human, Ct)).Value);
            return new SourceSync(RecordSamples.Linear, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, Code: null);
        });

        Assert.Equal(("idle", 0, 0), (idle.Pull, idle.Records, idle.Failures));
        Assert.Equal(("backoff", 1, ContextErrors.SourceUnavailable.Code), (backoff.Pull, backoff.Failures, backoff.LastError));
        Assert.Equal(clock.GetUtcNow(), backoff.NextTry);   // why: the clock has since moved the 5 minutes the first failure waits
        Assert.False(string.IsNullOrWhiteSpace(backoff.LastErrorText));
        Assert.Equal("idle", due.Pull);
        Assert.Equal("running", running?.Pull);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Five_failures_in_a_row_past_the_next_try_read_as_failed()
    {
        var store = new FakeRecordStore();
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 29, 12, 0, 0, TimeSpan.Zero));
        var index = Rig(store, clock, new SwitchFeed(RecordSamples.Linear) { Down = true }, out _);

        for (var i = 0; i < 5; i++)
        {
            _ = await index.Tick(MapWorld.Human, RecordSamples.Linear, Ct);
        }

        clock.Advance(TimeSpan.FromHours(7));
        Assert.Equal("failed", Assert.Single((await index.Progress(MapWorld.Human, Ct)).Value).Pull);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_backoff_and_last_commit_survive_a_restart()
    {
        using var home = new TempHome();
        var feed = new SwitchFeed(RecordSamples.Linear) { Down = true };
        var first = new SqliteRecordStore(home.Database);
        _ = await Rig(first, home.Clock, feed, out _).Tick(MapWorld.Human, Ct);
        var failedAt = home.Clock.GetUtcNow();

        var restarted = new SqliteRecordStore(home.Database);
        var row = Assert.Single((await Rig(restarted, home.Clock, feed, out _).Progress(MapWorld.Human, Ct)).Value);

        Assert.Equal(("backoff", 1, failedAt.AddMinutes(5)), (row.Pull, row.Failures, row.NextTry));
        Assert.Equal(ContextErrors.SourceUnavailable.Code, row.LastError);
        Assert.Null(row.LastCommit);
    }

    /// <summary>A feed that is down, or serves nothing, and counts its pulls.</summary>
    private sealed class SwitchFeed(SourceId source) : RecordFeed(source)
    {
        private int _pulls;

        public bool Down { get; set; }

        public int Pulls => Volatile.Read(ref _pulls);

        public override ValueTask<Result<RecordPage>> Pull(RecordPull pull, CancellationToken cancellationToken)
        {
            _ = Interlocked.Increment(ref _pulls);
            return Down
                ? ValueTask.FromResult<Result<RecordPage>>(ContextErrors.SourceUnavailable.With("the source is down"))
                : ValueTask.FromResult<Result<RecordPage>>(new RecordPage([], [], SourceCursor.From("0"), CursorReset: false));
        }
    }
}
