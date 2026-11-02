using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Context.Features.SyncSources;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Records;
using Aw.Kernel;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.SyncSources;

/// <summary>Backfill reads only what is older than where Recent started (#1512): the bound is stored when Recent ends, so a later tick on a later day still stops at the same instant.</summary>
[Trait("Lane", "unit")]
public sealed class RecordSyncRunBackfillBoundTests
{
    private static readonly SourceId _source = RecordSamples.Linear;
    private static readonly DateTimeOffset _recentStart = RecordSamples.Now.AddDays(-30);
    private readonly FakeRecordStore _store = new();
    private readonly FakeTimeProvider _clock = new(RecordSamples.Now);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static RecordItem Item(string id, int ageDays) => new(
        RecordKey.From($"linear:issue/{id}"), RecordKind.Issue, $"Title {id}", $"Text {id}", Author: null, Url: null, RecordState.Open, StateRaw: null, CreatedAt: null,
        RecordSamples.Now.AddDays(-ageDays), ClosedAt: null, SourceVersion.From($"v{id}"), Links: [], Repo: null, Audience: null);

    private RecordSyncRun Run(RecordFeed feed, int pageSize = RecordSyncRun.PageSize) =>
        new(feed, _store, (new FakeScrubberFactory().For([]).Value, new ContextIndexSettings()), _clock, pageSize);

    // The Backfill pulls: those with the window's start as lower bound, from the first up to and including the first that came back empty (Backfill ends on an empty
    // page and Incremental, which has the same lower bound, starts after it).
    private static List<RecordPull> Backfill(SpyFeed feed, DateTimeOffset now, int from = 0)
    {
        var window = now.AddDays(-ContextIndexSettings.DefaultWindowDays);
        var pulls = new List<RecordPull>();
        for (var i = from; i < feed.Pulls.Count; i++)
        {
            if (feed.Pulls[i].NotBefore != window)
            {
                continue;
            }

            pulls.Add(feed.Pulls[i]);
            if (feed.Returned[i] == 0)
            {
                break;
            }
        }

        return pulls;
    }

    [Fact]
    public async Task A_first_tick_over_items_updated_in_the_last_30_days_fetches_each_item_once()
    {
        var feed = new SpyFeed(new FakeRecordFeed(_source, [Item("a", 10), Item("b", 20)]));

        var row = await Run(feed).Execute(Ct);

        Assert.Equal(2, row.Fetched);
        var backfill = Backfill(feed, RecordSamples.Now);
        Assert.NotEmpty(backfill);
        Assert.All(backfill, p => Assert.Equal(_recentStart, p.Before));
    }

    [Fact]
    public async Task The_bound_is_the_Recent_start_as_stored_when_Recent_ended_not_recomputed_on_a_later_day()
    {
        var down = new SpyFeed(new FakeRecordFeed(_source, [Item("a", 10), Item("b", 20)])) { FailFrom = 3 };
        _ = await Run(down, pageSize: 1).Execute(Ct);   // Recent: a, b, an empty page; then Backfill's first pull fails
        Assert.Equal(SyncPhase.Backfill, (await _store.Position(_source, Ct)).Value.Phase);
        _clock.Advance(TimeSpan.FromDays(5));
        var healthy = new SpyFeed(new FakeRecordFeed(_source, [Item("a", 10), Item("b", 20)]));

        _ = await Run(healthy, pageSize: 1).Execute(Ct);

        Assert.Equal(_clock.GetUtcNow().AddDays(-ContextIndexSettings.DefaultWindowDays), healthy.Pulls[0].NotBefore);   // the first pull of the second run is a Backfill one
        var backfill = Backfill(healthy, _clock.GetUtcNow());
        Assert.NotEmpty(backfill);
        Assert.All(backfill, p => Assert.Equal(_recentStart, p.Before));
    }

    [Fact]
    public async Task Recent_starts_thirty_days_before_one_clock_reading()
    {
        var clock = new SteppingClock(RecordSamples.Now);
        var feed = new SpyFeed(new FakeRecordFeed(_source, [Item("a", 10)])) { Clock = clock };

        _ = await new RecordSyncRun(feed, _store, (new FakeScrubberFactory().For([]).Value, new ContextIndexSettings()), clock, RecordSyncRun.PageSize).Execute(Ct);

        // The readings before the first pull are the run's start, the cap check, the window's start and RecentStart's own: one, so the pull's bound is the last of them.
        // Two readings (the old Max of two) would make four before the pull and bound the Recent pull one step later than the window's start reading plus one.
        Assert.Equal(4, feed.ReadingsAtPull[0]);
        Assert.Equal(clock.Readings[3] - RecordSyncRun.RecentWindow, feed.Pulls[0].NotBefore);
    }

    /// <summary>A clock that moves forward one second on every reading, so two readings never agree.</summary>
    private sealed class SteppingClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _next = start;

        public List<DateTimeOffset> Readings { get; } = [];

        public override DateTimeOffset GetUtcNow()
        {
            var now = _next;
            Readings.Add(now);
            _next = now.AddSeconds(1);
            return now;
        }
    }

    /// <summary>Wraps a feed: records every pull and answers a SND-CONTEXT-010 error from pull number <see cref="FailFrom"/> on.</summary>
    private sealed class SpyFeed(RecordFeed inner) : RecordFeed(inner.Source)
    {
        public List<RecordPull> Pulls { get; } = [];

        /// <summary>How many items each pull in <see cref="Pulls"/> returned.</summary>
        public List<int> Returned { get; } = [];

        public int FailFrom { get; init; } = int.MaxValue;

        public SteppingClock? Clock { get; init; }

        /// <summary>How many clock readings had been taken when each pull arrived (needs <see cref="Clock"/>).</summary>
        public List<int> ReadingsAtPull { get; } = [];

        public override async ValueTask<Result<RecordPage>> Pull(RecordPull pull, CancellationToken cancellationToken)
        {
            Pulls.Add(pull);
            ReadingsAtPull.Add(Clock?.Readings.Count ?? 0);
            if (Pulls.Count - 1 >= FailFrom)
            {
                Returned.Add(0);
                return ContextErrors.SourceUnavailable.With("down");
            }

            var page = await inner.Pull(pull, cancellationToken).ConfigureAwait(false);
            Returned.Add(page.IsError ? 0 : page.Value.Items.Count);
            return page;
        }
    }
}
