using Aw.Sindri.Infrastructure.Ledger;
using Microsoft.Data.Sqlite;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>#2221: the ledger's and views.db's write-ahead logs are checkpointed on a tick, not left to SQLite's automatic checkpoint alone.</summary>
public sealed class SqliteStoreCheckpointsTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aw-sindri-wal-").FullName;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static SqliteConnection Open(string path)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        connection.Open();
        return connection;
    }

    private static void Run(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        _ = command.ExecuteNonQuery();
    }

    /// <summary>A store whose log has grown: automatic checkpoints off, so only a tick can reset it.</summary>
    private string GrownStore(string name)
    {
        var path = Path.Combine(_dir, name);
        using var connection = Open(path);
        Run(connection, "PRAGMA journal_mode=WAL");
        Run(connection, "PRAGMA wal_autocheckpoint=0");
        Run(connection, "CREATE TABLE t(id INTEGER PRIMARY KEY, v TEXT)");
        for (var i = 0; i < 200; i++)
        {
            Run(connection, "INSERT INTO t(v) VALUES (hex(randomblob(2000)))");
        }

        // closing the last connection would checkpoint; keep the log by copying nothing: reopen below holds it
        return path;
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_tick_truncates_the_log_of_the_ledger_and_the_views_store()
    {
        var ledger = GrownStore("ledger.db");
        var views = GrownStore("views.db");
        await using var holdLedger = Open(ledger);
        await using var holdViews = Open(views);
        Run(holdLedger, "PRAGMA wal_autocheckpoint=0");
        Run(holdViews, "PRAGMA wal_autocheckpoint=0");
        Run(holdLedger, "INSERT INTO t(v) VALUES (hex(randomblob(2000)))");
        Run(holdViews, "INSERT INTO t(v) VALUES (hex(randomblob(2000)))");
        Assert.True(new FileInfo(ledger + "-wal").Length > 0);
        Assert.True(new FileInfo(views + "-wal").Length > 0);

        var checkpoints = await new SqliteStoreCheckpoints([ledger, views]).Run(Ct);

        Assert.Equal(["ledger.db", "views.db"], checkpoints.Select(static c => c.Store), StringComparer.Ordinal);
        Assert.All(checkpoints, static c => Assert.True(c.Truncated));
        Assert.Equal(0, new FileInfo(ledger + "-wal").Length);
        Assert.Equal(0, new FileInfo(views + "-wal").Length);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_store_held_by_a_reader_is_partial_and_not_an_error()
    {
        var path = GrownStore("ledger.db");
        await using var reader = Open(path);
        Run(reader, "PRAGMA wal_autocheckpoint=0");
        Run(reader, "BEGIN");
        using (var select = reader.CreateCommand())
        {
            select.CommandText = "SELECT count(*) FROM t";
            _ = select.ExecuteScalar();
        }

        await using var writer = Open(path);
        Run(writer, "INSERT INTO t(v) VALUES ('after the reader began')");

        var checkpoints = await new SqliteStoreCheckpoints([path]).Run(Ct);

        var only = Assert.Single(checkpoints);
        Assert.False(only.Truncated);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_store_with_no_file_is_left_out()
    {
        var checkpoints = await new SqliteStoreCheckpoints([Path.Combine(_dir, "views.db")]).Run(Ct);

        Assert.Empty(checkpoints);
    }
}
