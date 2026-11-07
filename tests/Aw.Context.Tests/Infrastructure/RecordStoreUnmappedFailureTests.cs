using Aw.Context.Core;
using Aw.Context.Core.Records;
using Aw.Context.Testing.Records;

namespace Aw.Context.Tests.Infrastructure;

/// <summary>
/// Every public write of the record store returns a SQLite failure Kernel.Data does not map as SND-CONTEXT-004 and rolls back, never an exception (#2172). A trigger whose target table is gone fails with
/// SQLITE_ERROR (code 1), which Kernel.Data's SqliteErrors leaves unmapped.
/// </summary>
public sealed class RecordStoreUnmappedFailureTests : IDisposable
{
    private readonly RecordStoreWorld _world = new();

    public void Dispose() => _world.Dispose();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // why: the trigger's INSERT into a dropped table fails with SQLITE_ERROR when the trigger fires, not when it is created.
    private async Task Plant(string whenOn)
    {
        await _world.Migrate();
        _world.Sql($"CREATE TABLE gone(x); CREATE TRIGGER broken AFTER {whenOn} BEGIN INSERT INTO gone VALUES (1); END; DROP TABLE gone;");
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Commit_returns_an_unmapped_sqlite_failure_as_an_error_and_rolls_back()
    {
        await Plant("INSERT ON source_record");

        var committed = await _world.Store.Commit(RecordSamples.Batch(RecordSamples.Linear, 1, RecordSamples.Write(RecordSamples.Record("linear:issue/1"), "text")), Ct);

        Assert.True(committed.IsError);
        Assert.Equal(ContextErrors.StoreUnavailable.Code, committed.Error.Code);
        Assert.Equal(0, _world.Count("SELECT count(*) FROM source_record"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task RecordOutcome_returns_an_unmapped_sqlite_failure_as_an_error()
    {
        await Plant("INSERT ON source_cursor");

        var saved = await _world.Store.RecordOutcome(RecordSamples.Linear, new SyncOutcome(RecordSamples.Now, Code: null), Ct);

        Assert.True(saved.IsError);
        Assert.Equal(ContextErrors.StoreUnavailable.Code, saved.Error.Code);
        Assert.Equal(0, _world.Count("SELECT count(*) FROM source_cursor"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task RecordAttempt_returns_an_unmapped_sqlite_failure_as_an_error()
    {
        await Plant("INSERT ON source_cursor");

        var saved = await _world.Store.RecordAttempt(RecordSamples.Linear, new SyncAttempt(RecordSamples.Now, Failed: true, "down"), Ct);

        Assert.True(saved.IsError);
        Assert.Equal(ContextErrors.StoreUnavailable.Code, saved.Error.Code);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Purge_returns_an_unmapped_sqlite_failure_as_an_error_and_keeps_the_records()
    {
        _ = await _world.Store.Commit(RecordSamples.Batch(RecordSamples.Linear, 1, RecordSamples.Write(RecordSamples.Record("linear:issue/1"), "text")), Ct);
        await Plant("DELETE ON source_record");

        var purged = await _world.Store.Purge(RecordSamples.Linear, Ct);

        Assert.True(purged.IsError);
        Assert.Equal(ContextErrors.StoreUnavailable.Code, purged.Error.Code);
        Assert.Equal(1, _world.Count("SELECT count(*) FROM source_record"));
    }
}
