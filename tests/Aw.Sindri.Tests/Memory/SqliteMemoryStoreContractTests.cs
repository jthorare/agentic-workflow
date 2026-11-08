using Aw.Kernel.Testing;
using Aw.Sindri.Core.Memory;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Ledger;
using Aw.Sindri.Infrastructure.Memory;
using Aw.Sindri.Testing.Memory;
using Aw.Sindri.Tests.Ledger;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Memory;

/// <summary>The 'memory' and 'memory_fact' tables of ledger.db (migration 9) against the store contract, on a fresh file.</summary>
[Lane(TestLane.Integration)]
public sealed class SqliteMemoryStoreContractTests : MemoryStoreContract, IDisposable
{
    private readonly TempHome _home = new();
    private readonly List<LedgerDatabase> _databases = [];

    public void Dispose()
    {
        foreach (var database in _databases)
        {
            database.Dispose();
        }

        _home.Dispose();
    }

    protected override IMemoryStore Create()
    {
        var database = new LedgerDatabase(new SindriSettings(_home.Path, Path.Combine(_home.Path, "profile.yaml")), new FakeTimeProvider(KnownInstants.Epoch));
        _databases.Add(database);
        return new SqliteMemoryStore(database);
    }

    [Fact]
    public void The_migration_that_makes_the_tables_is_named_memory()
        => Assert.Contains(LedgerDatabase.Migrations, static m => m.Name.Contains("memory", StringComparison.Ordinal));

    [Fact]
    public async Task The_embedding_is_a_blob_and_the_text_is_what_the_service_gave()
    {
        var store = Create();
        _ = await store.Create(MemorySamples.Info(), TestContext.Current.CancellationToken);
        _ = await store.Add(MemorySamples.Fact(vector: [0.5f, 0.5f, 0.5f, 0.5f]), TestContext.Current.CancellationToken);

        var file = Path.Combine(_home.Path, "ledger.db");
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={file};Mode=ReadOnly;Pooling=False");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT typeof(embedding) FROM memory_fact";
        Assert.Equal("blob", (string?)await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));
    }
}
