using Aw.Context.Infrastructure.Graph;
using Aw.Context.Infrastructure.Store;
using Aw.Context.Testing.Map;
using Aw.Context.Tests.Map;
using Aw.Kernel.Data;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.RunUpkeep;

/// <summary>The write-ahead log of context.db is capped by the connections the product opens, so a log that grew under a reader shrinks on its own (#2022).</summary>
[Trait("Lane", "integration")]
public sealed class StoreLogCapTests : IDisposable
{
    private const long LogCapBytes = 4 * 1024 * 1024;

    private readonly TempHome _home = new();
    private readonly ContextStoreSettings _settings;
    private readonly ContextDatabase _database;

    public StoreLogCapTests()
    {
        _settings = SettingsFromEnvironment.Read(_home.Environment);
        _database = new ContextDatabase(_settings, new FakeTimeProvider());
    }

    public void Dispose()
    {
        _database.Dispose();
        SqliteConnection.ClearAllPools();
        _home.Dispose();
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string Text(long value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private long WalBytes => File.Exists(_settings.DatabasePath + "-wal") ? new FileInfo(_settings.DatabasePath + "-wal").Length : 0;

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _settings.DatabasePath, Mode = SqliteOpenMode.ReadWrite, Pooling = false }.ToString());
        connection.Open();
        return connection;
    }

    private static object? Scalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    private async Task WriteMap() =>
        _ = await new SqliteGraphStore(_database, _settings).SaveCurrent(SampleMap.Build(), SampleMap.At, Ct);

    /// <summary>A connection from the factory the store itself opens its connections with.</summary>
    private async Task<SqliteConnection> OpenLikeTheStore()
    {
        var opened = await new SqliteConnectionFactory(new SqliteOptions(_settings.DatabasePath)).Open(Ct);
        Assert.True(opened.IsSuccess);
        return opened.Value;
    }

    [Fact]
    public async Task The_store_caps_its_write_ahead_log()
    {
        await WriteMap();

        await using var connection = await OpenLikeTheStore();

        var limit = Convert.ToInt64(Scalar(connection, "PRAGMA journal_size_limit"), System.Globalization.CultureInfo.InvariantCulture);
        Assert.True(limit > 0, $"journal_size_limit is {Text(limit)}: the log has no size cap");
    }

    [Fact]
    public async Task A_log_that_grew_under_a_reader_shrinks_under_the_cap_once_readers_move_on()
    {
        await WriteMap();
        await using var reader = Open();
        _ = Scalar(reader, "BEGIN");
        _ = Scalar(reader, "SELECT count(*) FROM sqlite_master");

        await using (var grower = Open())
        {
            _ = Scalar(grower, "PRAGMA busy_timeout = 5000");
            _ = Scalar(grower, "CREATE TABLE log_cap_probe (payload BLOB)");
            _ = Scalar(grower, "BEGIN");
            for (var i = 0; i < 16; i++)
            {
                _ = Scalar(grower, "INSERT INTO log_cap_probe SELECT randomblob(1024) FROM (WITH RECURSIVE n(i) AS (SELECT 1 UNION ALL SELECT i + 1 FROM n WHERE i < 1024) SELECT i FROM n)");
            }

            _ = Scalar(grower, "COMMIT");
        }

        var peak = WalBytes;
        Assert.True(peak > 2 * LogCapBytes, $"the setup only grew the log to {Text(peak)} bytes");

        _ = Scalar(reader, "COMMIT");
        await using (var writer = await OpenLikeTheStore())
        {
            _ = Scalar(writer, "PRAGMA wal_checkpoint(RESTART)");
            _ = Scalar(writer, "INSERT INTO log_cap_probe VALUES (randomblob(16))");
        }

        Assert.True(WalBytes <= LogCapBytes, $"the log is {Text(WalBytes)} bytes after the readers moved on and it restarted (peak {Text(peak)}), cap {Text(LogCapBytes)}");
    }
}
