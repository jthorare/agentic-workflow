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

/// <summary>A feed that asks for the guard has its pages committed with it (Linear hub spec 2026-11-04 D4): a page fetched before a newer write cannot put the old value back.</summary>
[Trait("Lane", "unit")]
public sealed class RecordSyncRunGuardTests
{
    private static readonly SourceId _source = RecordSamples.Linear;
    private readonly FakeRecordStore _store = new();
    private readonly FakeTimeProvider _clock = new(RecordSamples.Now);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static RecordItem Item(string title, DateTimeOffset updatedAt) => new(
        RecordKey.From("linear:issue/a"), RecordKind.Issue, title, "Text", Author: null, Url: null, RecordState.Open, StateRaw: null, CreatedAt: null,
        updatedAt, ClosedAt: null, SourceVersion.From("v-" + title), Links: [], Repo: null, Audience: null);

    private async Task Store(RecordItem item)
    {
        var record = RecordSamples.Record(item.Key.Value, item.Version.Value, _source) with { Title = item.Title, UpdatedAt = item.UpdatedAt };
        _ = await _store.Commit(RecordSamples.Batch(_source, 0, RecordSamples.Write(record, "stored words")), Ct);
    }

    private RecordSyncRun Run(RecordFeed feed) =>
        new(feed, _store, (new FakeScrubberFactory().For([]).Value, new ContextIndexSettings()), _clock);

    [Fact]
    public async Task A_guarded_feeds_older_page_does_not_replace_a_newer_stored_record()
    {
        await Store(Item("saved", RecordSamples.Now.AddHours(-1)));
        var feed = new GuardedFeed(guard: true, Item("stale", RecordSamples.Now.AddHours(-2)));

        var row = await Run(feed).Execute(Ct);

        Assert.Equal("saved", (await _store.Get(RecordKey.From("linear:issue/a"), Ct)).Value!.Title);
        Assert.Equal(0, row.Records);
        Assert.Equal(1, row.Unchanged);
    }

    [Fact]
    public async Task A_guarded_feeds_equal_or_newer_page_replaces_the_stored_record()
    {
        await Store(Item("saved", RecordSamples.Now.AddHours(-1)));
        var feed = new GuardedFeed(guard: true, Item("same time", RecordSamples.Now.AddHours(-1)));

        _ = await Run(feed).Execute(Ct);

        Assert.Equal("same time", (await _store.Get(RecordKey.From("linear:issue/a"), Ct)).Value!.Title);
    }

    [Fact]
    public async Task An_unguarded_feed_writes_the_older_page()
    {
        await Store(Item("saved", RecordSamples.Now.AddHours(-1)));
        var feed = new GuardedFeed(guard: false, Item("stale", RecordSamples.Now.AddHours(-2)));

        _ = await Run(feed).Execute(Ct);

        Assert.Equal("stale", (await _store.Get(RecordKey.From("linear:issue/a"), Ct)).Value!.Title);
    }

    private sealed class GuardedFeed(bool guard, RecordItem item) : RecordFeed(_source)
    {
        private readonly FakeRecordFeed _inner = new(_source, [item]);

        public override bool Guard => guard;

        public override ValueTask<Result<RecordPage>> Pull(RecordPull pull, CancellationToken cancellationToken) => _inner.Pull(pull, cancellationToken);
    }
}
