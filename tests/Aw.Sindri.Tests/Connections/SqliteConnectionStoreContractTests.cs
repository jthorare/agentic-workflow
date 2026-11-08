using Aw.Kernel.Testing;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Connections;
using Aw.Sindri.Infrastructure.Ledger;
using Aw.Sindri.Testing.Connections;
using Aw.Sindri.Tests.Ledger;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Connections;

/// <summary>The 'connection' table of ledger.db (migration 8) against the store contract, on a fresh file.</summary>
[Lane(TestLane.Integration)]
public sealed class SqliteConnectionStoreContractTests : ConnectionStoreContract, IDisposable
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

    protected override IConnectionStore Create()
    {
        var database = new LedgerDatabase(new SindriSettings(_home.Path, Path.Combine(_home.Path, "profile.yaml")), new FakeTimeProvider(KnownInstants.Epoch));
        _databases.Add(database);
        return new SqliteConnectionStore(database);
    }

    [Fact]
    public void The_migration_that_makes_the_table_is_number_9_named_connections_and_reads_start_there()
    {
        Assert.Contains(LedgerDatabase.Migrations, static m => m.Version == 9 && m.Name.Contains("connections", StringComparison.Ordinal));
        Assert.Equal(10, LedgerDatabase.ConnectionsVersion);   // why: the constant the store's reads wait for is the migration that makes the table
    }

    [Fact]
    public async Task The_row_holds_the_pointer_and_no_token_column()
    {
        var store = Create();
        _ = await store.Upsert(ConnectionSamples.Oauth(), TestContext.Current.CancellationToken);

        var file = Path.Combine(_home.Path, "ledger.db");
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={file};Mode=ReadOnly;Pooling=False");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM pragma_table_info('connection')";
        var columns = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken))
        {
            while (await reader.ReadAsync(TestContext.Current.CancellationToken))
            {
                columns.Add(reader.GetString(0));
            }
        }

        Assert.Contains("secret_pointer", columns, StringComparer.Ordinal);
        Assert.DoesNotContain(columns, static c => c.Contains("token", StringComparison.OrdinalIgnoreCase) || string.Equals(c, "secret", StringComparison.Ordinal));
    }
}
