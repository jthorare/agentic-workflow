using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Records;

namespace Aw.Context.Tests.Infrastructure;

/// <summary>
/// A page is all or none (spec §5.5): for each pair the page writes together, a trigger planted in the file makes the second write fail, as a kill between them would, and the first must not stay.
/// The same page then commits once the trigger is gone, so a retry after the failure is whole.
/// </summary>
public sealed class SqliteRecordStoreAtomicityTests : IDisposable
{
    private readonly RecordStoreWorld _world = new();

    public void Dispose() => _world.Dispose();

    [Fact, Trait("Lane", "integration")]
    public Task A_record_and_its_cursor_commit_together() =>
        Killed("CREATE TRIGGER kill BEFORE INSERT ON source_cursor BEGIN SELECT RAISE(ABORT, 'killed'); END", "kill");

    [Fact, Trait("Lane", "integration")]
    public Task A_record_and_its_chunks_commit_together() =>
        Killed("CREATE TRIGGER kill BEFORE INSERT ON chunk BEGIN SELECT RAISE(ABORT, 'killed'); END", "kill");

    [Fact, Trait("Lane", "integration")]
    public Task A_record_and_its_intent_row_commit_together() =>
        Killed("CREATE TRIGGER kill BEFORE INSERT ON intent_record BEGIN SELECT RAISE(ABORT, 'killed'); END", "kill");

    [Fact, Trait("Lane", "integration")]
    public Task A_record_and_its_links_commit_together() =>
        Killed("CREATE TRIGGER kill BEFORE INSERT ON record_link BEGIN SELECT RAISE(ABORT, 'killed'); END", "kill");

    [Fact, Trait("Lane", "integration")]
    public async Task A_tombstone_and_its_cursor_commit_together()
    {
        var record = RecordSamples.Record("linear:issue/1");
        await _world.Migrate();
        _ = await _world.Store.Commit(RecordSamples.Batch(RecordSamples.Linear, 1, RecordSamples.Write(record, "words")), TestContext.Current.CancellationToken);
        _world.Sql("CREATE TRIGGER kill BEFORE UPDATE ON source_cursor BEGIN SELECT RAISE(ABORT, 'killed'); END");

        var refused = await _world.Store.Commit(RecordSamples.Batch(RecordSamples.Linear, 2) with { Removed = [record.Key] }, TestContext.Current.CancellationToken);

        Assert.True(refused.IsError);
        Assert.Null((await _world.Store.Get(record.Key, TestContext.Current.CancellationToken)).Value!.RemovedAt);
        _ = Assert.Single((await _world.Store.Chunks(record.Key, TestContext.Current.CancellationToken)).Value);
        Assert.Equal(SourceCursor.From("1"), (await _world.Store.Position(RecordSamples.Linear, TestContext.Current.CancellationToken)).Value.Cursor);
    }

    private async Task Killed(string plant, string trigger)
    {
        await _world.Migrate();
        _world.Sql(plant);
        var record = RecordSamples.Record("linear:issue/1") with { Links = [new RecordLink("linear:issue/2", RecordLinkKind.Relates, "Two")] };
        var batch = RecordSamples.Batch(RecordSamples.Linear, 1, RecordSamples.Write(record, "words"));

        var refused = await _world.Store.Commit(batch, TestContext.Current.CancellationToken);

        Assert.True(refused.IsError);
        Assert.Null((await _world.Store.Get(record.Key, TestContext.Current.CancellationToken)).Value);
        Assert.Empty((await _world.Store.Chunks(record.Key, TestContext.Current.CancellationToken)).Value);
        Assert.Equal(SyncPosition.Start, (await _world.Store.Position(RecordSamples.Linear, TestContext.Current.CancellationToken)).Value);
        Assert.Equal(0, _world.Count("SELECT record_stamp FROM store_epoch"));
        Assert.Equal(0, _world.Count("SELECT count(*) FROM intent_record"));
        Assert.Equal(0, _world.Count("SELECT count(*) FROM record_link"));

        _world.Sql($"DROP TRIGGER {trigger}");
        Assert.Equal(new CommitReport(1, 0, 0), (await _world.Store.Commit(batch, TestContext.Current.CancellationToken)).Value);
    }
}
