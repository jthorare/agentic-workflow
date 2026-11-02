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

/// <summary>The record sync driver (owned-index spec §5.5, U8): phase choice, the window bound, resuming from the stored position, replays, a feed that is down and cancellation.</summary>
[Trait("Lane", "unit")]
public sealed class RecordSyncRunTests
{
    private static readonly SourceId _source = RecordSamples.Linear;
    private readonly FakeRecordStore _store = new();
    private readonly FakeTimeProvider _clock = new(RecordSamples.Now);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Newest first, as a feed serves them: 10, 100 and 300 days old (the last is outside the default 183-day window).
    private static List<RecordItem> Items() => [Item("a", 10), Item("b", 100), Item("c", 300)];

    private static RecordItem Item(string id, int ageDays) => new(
        RecordKey.From($"linear:issue/{id}"), RecordKind.Issue, $"Title {id}", $"Text {id}", Author: null, Url: null, RecordState.Open, StateRaw: null, CreatedAt: null,
        RecordSamples.Now.AddDays(-ageDays), ClosedAt: null, SourceVersion.From($"v{id}"), Links: [], Repo: null, Audience: null);

    private RecordSyncRun Run(RecordFeed feed, int windowDays = ContextIndexSettings.DefaultWindowDays, int pageSize = RecordSyncRun.PageSize) =>
        new(feed, _store, (new FakeScrubberFactory().For([]).Value, new ContextIndexSettings { WindowDays = windowDays }), _clock, pageSize);

    private async Task<List<string>> Keys() =>
        [.. (await _store.List(new RecordFilter(), after: null, 1000, Ct)).Value.Select(r => r.Key.Value)];

    [Fact]
    public async Task Tick_walks_recent_then_backfill_then_incremental_and_stores_only_the_window()
    {
        var feed = new SpyFeed(new FakeRecordFeed(_source, Items()));

        var row = await Run(feed).Execute(Ct);

        Assert.Null(row.Code);
        Assert.Equal(["linear:issue/a", "linear:issue/b"], await Keys());
        var position = (await _store.Position(_source, Ct)).Value;
        Assert.Equal(SyncPhase.Incremental, position.Phase);
        var window = RecordSamples.Now.AddDays(-ContextIndexSettings.DefaultWindowDays);
        var recent = RecordSamples.Now.AddDays(-30);
        Assert.Equal<(string?, DateTimeOffset, DateTimeOffset?)[]>(
            [
                (null, recent, null),                     // Recent: the last 30 days
                ("1", recent, null),                      // its end: an empty page
                (null, window, recent),   // Backfill from its own cursor, to the window's start, stopping where Recent started
                ("1", window, recent),    // ... its end: an empty page
                ("1", window, null),      // Incremental from the cursor Recent left
                ("2", window, null),
            ],
            [.. feed.Pulls.Select(p => (p.After?.Value, p.NotBefore!.Value, p.Before))]);
    }

    [Fact]
    public async Task Nothing_older_than_the_window_is_ever_pulled()
    {
        var feed = new SpyFeed(new FakeRecordFeed(_source, Items()));

        _ = await Run(feed, windowDays: 20).Execute(Ct);

        Assert.Equal(["linear:issue/a"], await Keys());
        Assert.All(feed.Pulls, p => Assert.True(p.NotBefore >= RecordSamples.Now.AddDays(-20)));
    }

    [Fact]
    public async Task A_resumed_tick_continues_from_the_stored_position()
    {
        var down = new SpyFeed(new FakeRecordFeed(_source, Items())) { FailFrom = 2 };
        var first = await Run(down).Execute(Ct);
        var stored = (await _store.Position(_source, Ct)).Value;
        var healthy = new SpyFeed(new FakeRecordFeed(_source, Items()));

        var second = await Run(healthy).Execute(Ct);

        Assert.Equal(ContextErrors.SourceUnavailable.Code, first.Code);
        Assert.Equal((SourceCursor.From("1"), SyncPhase.Backfill, null), (stored.Cursor, stored.Phase, stored.BackfillCursor));
        Assert.Null(second.Code);
        Assert.Null(healthy.Pulls[0].After);   // Backfill resumes from its own (still empty) cursor, not Recent's
        Assert.Equal(["linear:issue/a", "linear:issue/b"], await Keys());
    }

    [Fact]
    public async Task A_caught_up_source_commits_nothing_and_a_replayed_page_changes_nothing()
    {
        _ = await Run(new FakeRecordFeed(_source, Items())).Execute(Ct);
        var before = (await _store.List(new RecordFilter(), after: null, 1000, Ct)).Value;

        var quiet = await Run(new FakeRecordFeed(_source, Items())).Execute(Ct);
        _ = await _store.Commit(RecordSamples.Batch(_source, 0), Ct);   // the position goes back to the start, as a feed that reset its cursor would leave it
        var replay = await Run(new FakeRecordFeed(_source, Items())).Execute(Ct);

        Assert.Equal((0, 0, null), (quiet.Fetched, quiet.Records, quiet.Code));
        Assert.Equal((0, 2), (replay.Records, replay.Unchanged));
        Assert.Equal(before, (await _store.List(new RecordFilter(), after: null, 1000, Ct)).Value);
    }

    [Fact]
    public async Task A_feed_that_is_down_is_a_row_with_its_code()
    {
        var feed = new SpyFeed(new FakeRecordFeed(_source, Items())) { FailFrom = 0 };

        var row = await Run(feed).Execute(Ct);

        Assert.Equal((_source, 0, ContextErrors.SourceUnavailable.Code), (row.Source, row.Records, row.Code));
        Assert.Empty(await Keys());
    }

    [Fact]
    public async Task A_tick_commits_at_most_PagesPerTick_pages()
    {
        var many = Enumerable.Range(0, RecordSyncRun.PagesPerTick + 5).Select(i => Item(i.ToString("D3", System.Globalization.CultureInfo.InvariantCulture), 1)).ToList();

        var row = await Run(new FakeRecordFeed(_source, many), pageSize: 1).Execute(Ct);

        Assert.Equal((RecordSyncRun.PagesPerTick, null), (row.Records, row.Code));
    }

    [Fact]
    public async Task Cancelling_between_pages_stops_the_tick_and_keeps_the_committed_page()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var feed = new SpyFeed(new FakeRecordFeed(_source, Items())) { CancelOnPull = (1, cts) };

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await Run(feed, pageSize: 1).Execute(cts.Token));

        Assert.Equal(["linear:issue/a"], await Keys());
        Assert.Equal(SourceCursor.From("1"), (await _store.Position(_source, Ct)).Value.Cursor);
    }

    /// <summary>Wraps a feed: records every pull, answers a SND-CONTEXT-010 error from pull number <see cref="FailFrom"/> on, and cancels a token on a given pull.</summary>
    private sealed class SpyFeed(RecordFeed inner) : RecordFeed(inner.Source)
    {
        public List<RecordPull> Pulls { get; } = [];

        public int FailFrom { get; init; } = int.MaxValue;

        public (int Pull, CancellationTokenSource Source)? CancelOnPull { get; init; }

        public override ValueTask<Result<RecordPage>> Pull(RecordPull pull, CancellationToken cancellationToken)
        {
            Pulls.Add(pull);
            if (CancelOnPull is { } cancel && Pulls.Count - 1 == cancel.Pull)
            {
                _ = cancel.Source.CancelAsync();
            }

            return Pulls.Count - 1 >= FailFrom
                ? ValueTask.FromResult<Result<RecordPage>>(ContextErrors.SourceUnavailable.With("down"))
                : inner.Pull(pull, cancellationToken);
        }
    }
}
