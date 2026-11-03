using System.Diagnostics;
using Aw.Context.Infrastructure.Graph;
using Aw.Context.Infrastructure.Store;
using Aw.Context.Testing.Map;
using Aw.Context.Tests.Map;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.RunUpkeep;

/// <summary>ContextDatabase.Upkeep on a real context.db: the planner stats and the log checkpoint, never waiting on a reader (#1953).</summary>
[Trait("Lane", "integration")]
public sealed class StoreUpkeepTests : IDisposable
{
    private readonly TempHome _home = new();
    private readonly ContextStoreSettings _settings;
    private readonly ContextDatabase _database;

    public StoreUpkeepTests()
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

    [Fact]
    public void No_store_is_no_upkeep()
    {
        var result = _database.Upkeep();

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.False(File.Exists(_settings.DatabasePath));
    }

    [Fact]
    public async Task A_store_with_no_readers_has_its_log_truncated_and_gets_planner_stats()
    {
        await WriteMap();

        var result = _database.Upkeep();

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.Truncated);
        Assert.True(result.Value.Optimized);
        Assert.Equal(0, WalBytes);
        await using var connection = Open();
        Assert.Equal(1L, Scalar(connection, "SELECT count(*) FROM sqlite_master WHERE name = 'sqlite_stat1'"));
    }

    [Fact]
    public async Task A_reader_holding_the_log_makes_the_pass_partial_without_waiting_and_a_later_pass_truncates()
    {
        await WriteMap();
        await using var reader = Open();
        await using var write = Open();
        _ = Scalar(reader, "PRAGMA journal_mode");
        _ = Scalar(reader, "BEGIN");
        var before = Scalar(reader, "SELECT count(*) FROM sqlite_master");
        _ = Scalar(write, "CREATE TABLE upkeep_probe (x INTEGER)");
        _ = Scalar(write, "INSERT INTO upkeep_probe VALUES (1)");

        var clock = Stopwatch.StartNew();
        var partial = _database.Upkeep();
        clock.Stop();

        Assert.True(partial.IsSuccess);
        Assert.False(partial.Value!.Truncated);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(2), $"the pass waited {clock.Elapsed}");
        Assert.Equal(before, Scalar(reader, "SELECT count(*) FROM sqlite_master"));

        _ = Scalar(reader, "COMMIT");
        var after = _database.Upkeep();

        Assert.True(after.Value!.Truncated);
        Assert.Equal(0, WalBytes);
    }
}
