using Aw.Kernel.Testing;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Connections;
using Aw.Sindri.Infrastructure.Ledger;
using Aw.Sindri.Tests.Ledger;
using Aw.Sindri.Testing.Connections;
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
    public void The_migration_that_makes_the_table_is_the_next_one_after_the_pr_comment_marks()
        => Assert.Contains(LedgerDatabase.Migrations, static m => m.Version == 8);

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
