using Aw.Context.Core;
using Aw.Context.Core.Records;
using Aw.Context.Core.Results;
using Aw.Context.Features.SyncSources;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Records;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.Sync;

/// <summary>The index purge (owned-index spec §5.8) and the per-source lock it shares with the tick: a purge never races a sync.</summary>
public sealed class IndexSyncPurgeTests
{
    private readonly FakeRecordStore _store = new();
    private readonly RecordFeedSync _feeds;
    private readonly IndexSync _index;

    public IndexSyncPurgeTests()
    {
        _feeds = new RecordFeedSync(new FakeRecordFeedCatalog(), _store, new FakeTimeProvider());
        _index = new IndexSync(_feeds, _store, new FakeScrubberFactory());
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task Seed()
    {
        var record = RecordSamples.Record("linear:issue/1");
        _ = await _store.Commit(RecordSamples.Batch(RecordSamples.Linear, 1, RecordSamples.Write(record, "words")), Ct);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Purge_deletes_the_source_and_counts_are_zero_after()
    {
        await Seed();

        var purged = await _index.Purge(RecordSamples.Linear, Ct);

        Assert.True(purged.IsSuccess);
        Assert.Equal(1, purged.Value);
        Assert.Equal(new SourceCounts(0, 0, 0), (await _index.Counts(RecordSamples.Linear, Ct)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Purge_while_a_tick_of_that_source_runs_is_SND_CONTEXT_056_and_deletes_nothing()
    {
        await Seed();
        Kernel.Error? refusal = null;

        var held = await _feeds.Locked(RecordSamples.Linear, async () =>
        {
            var attempt = await _index.Purge(RecordSamples.Linear, Ct);
            refusal = attempt.IsError ? attempt.Error : null;
            return new SourceSync(RecordSamples.Linear, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, Code: null);
        });

        Assert.True(held.IsSuccess);
        Assert.Equal(ContextErrors.SyncRunning.Code, refusal?.Code);
        Assert.Equal(1, (await _index.Counts(RecordSamples.Linear, Ct)).Value.Records);
    }
}
