using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Infrastructure.Store;
using Aw.Kernel.Data;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.Infrastructure;

/// <summary>Migrations 0023 and 0029 mark a row for a re-read with version ''; the store must still read it back (#1952, #1954).</summary>
public sealed class RereadMarkerReadTests : IDisposable
{
    private readonly RecordStoreWorld _world = new();

    public void Dispose() => _world.Dispose();

    [Fact, Trait("Lane", "integration")]
    public async Task A_record_waiting_for_its_reread_reads_back_instead_of_throwing()
    {
        var ct = TestContext.Current.CancellationToken;
        var runner = new MigrationRunner(new SqliteConnectionFactory(new SqliteOptions(_world.DatabasePath)), new FakeTimeProvider());
        _ = Directory.CreateDirectory(Path.GetDirectoryName(_world.DatabasePath) ?? ".");
        _ = await runner.Migrate([.. ContextDatabase.Migrations.Take(28)], ct);
        _world.Sql("INSERT INTO source_record (key, source, kind, version, text_hash, ruleset, blocked, synced_at, title) VALUES ('github:pr/o/r/1', 'github', 'pr', 'keep', 'h', 'r', 0, '2026-10-27T00:00:00Z', 't')");
        _ = await runner.Migrate(ContextDatabase.Migrations, ct);
        Assert.Equal(1, _world.Count("SELECT count(*) FROM source_record WHERE version = '' AND key = 'github:pr/o/r/1'"));

        var got = await _world.Store.Get(RecordKey.From("github:pr/o/r/1"), ct);

        Assert.False(got.IsError);
        var record = Assert.IsType<StoredRecord>(got.Value);
        Assert.NotEmpty(record.Version.Value);
        Assert.NotEqual("keep", record.Version.Value, StringComparer.Ordinal);

        var listed = await _world.Store.List(new RecordFilter(), after: null, 10, ct);

        Assert.False(listed.IsError);
        var listedRecord = Assert.Single(listed.Value);
        Assert.NotEmpty(listedRecord.Version.Value);
        Assert.NotEqual("keep", listedRecord.Version.Value, StringComparer.Ordinal);
    }
}
