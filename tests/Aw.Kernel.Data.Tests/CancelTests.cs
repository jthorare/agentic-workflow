using System.Diagnostics;
using Microsoft.Data.Sqlite;

namespace Aw.Kernel.Data.Tests;

/// <summary>
/// A cancelled read stops its statement (#1951): Microsoft.Data.Sqlite checks the token only between steps, and one step of an
/// aggregate is the whole scan, so without an interrupt an abandoned read runs to the end however long that takes.
/// </summary>
[Trait("Lane", "integration")]
public sealed class CancelTests
{
    // why: a count over 10^11 generated rows takes far longer than any test; only an interrupt ends it in time
    private const string Endless = "WITH RECURSIVE n(i) AS (SELECT 1 UNION ALL SELECT i + 1 FROM n WHERE i < 100000000000) SELECT COUNT(*) FROM n";

    private static readonly TimeSpan _grace = TimeSpan.FromSeconds(10);

    [Fact]
    public Task List_stops_its_statement_when_the_token_is_cancelled() =>
        AssertStops((session, token) => SqliteQueries.List(session, new SqlStatement(Endless), CountMapper.Instance, token).AsTask());

    [Fact]
    public Task One_stops_its_statement_when_the_token_is_cancelled() =>
        AssertStops((session, token) => SqliteQueries.One(session, new SqlStatement(Endless), CountMapper.Instance, token).AsTask());

    [Fact]
    public Task Exists_stops_its_statement_when_the_token_is_cancelled() =>
        AssertStops((session, token) => SqliteQueries.Exists(session, new SqlStatement(Endless), token).AsTask());

    [Fact]
    public async Task A_finished_read_leaves_the_connection_usable_after_its_token_is_cancelled()
    {
        using var db = new TempDatabase();
        await using var session = new ConnectionSession(await Open(db));
        using var cancel = new CancellationTokenSource();

        var first = await SqliteQueries.Exists(session, new SqlStatement("SELECT 1"), cancel.Token);
        await cancel.CancelAsync();
        var second = await SqliteQueries.Exists(session, new SqlStatement("SELECT 1"), TestContext.Current.CancellationToken);

        Assert.True(first.Value);
        Assert.True(second.Value);
    }

    [Fact]
    public async Task A_cancel_while_One_maps_its_row_gives_the_row_or_a_cancel_and_the_connection_still_reads()
    {
        using var db = new TempDatabase();
        await using var session = new ConnectionSession(await Open(db));
        using var cancel = new CancellationTokenSource();

        var read = await Record.ExceptionAsync(async () => _ = await SqliteQueries.One(session, new SqlStatement("SELECT 1"), new CancellingMapper(cancel), cancel.Token));

        Assert.True(read is null or OperationCanceledException, $"got {read?.GetType().Name}");
        Assert.True((await SqliteQueries.Exists(session, new SqlStatement("SELECT 1"), TestContext.Current.CancellationToken)).Value);
    }

    [Fact]
    public async Task A_cancel_while_List_maps_a_row_gives_the_rows_or_a_cancel_and_the_connection_still_reads()
    {
        using var db = new TempDatabase();
        await using var session = new ConnectionSession(await Open(db));
        using var cancel = new CancellationTokenSource();

        var read = await Record.ExceptionAsync(async () => _ = await SqliteQueries.List(session, new SqlStatement("SELECT 1 UNION ALL SELECT 2"), new CancellingMapper(cancel), cancel.Token));

        Assert.True(read is null or OperationCanceledException, $"got {read?.GetType().Name}");
        Assert.True((await SqliteQueries.Exists(session, new SqlStatement("SELECT 1"), TestContext.Current.CancellationToken)).Value);
    }

    private static async Task AssertStops<T>(Func<ISqliteSession, CancellationToken, Task<T>> read)
    {
        using var db = new TempDatabase();
        await using var session = new ConnectionSession(await Open(db));
        using var cancel = new CancellationTokenSource();

        // why: the statement runs synchronously inside the call, so it needs its own thread for the test to cancel it
        var running = Task.Run(() => read(session, cancel.Token), TestContext.Current.CancellationToken);
        await Task.Delay(TimeSpan.FromMilliseconds(300), TestContext.Current.CancellationToken);
        var clock = Stopwatch.StartNew();
        await cancel.CancelAsync();
        var finished = await Task.WhenAny(running, Task.Delay(_grace, TestContext.Current.CancellationToken));
        if (finished != running)
        {
            // why: end the runaway statement so the failed test does not keep a core busy
            SQLitePCL.raw.sqlite3_interrupt(session.Connection.Handle);
            _ = await Record.ExceptionAsync(() => running);
        }

        Assert.Same(running, finished);
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
        Assert.True(clock.Elapsed < _grace, $"the read took {clock.Elapsed} to stop after the cancel");
    }

    private static async Task<SqliteConnection> Open(TempDatabase db)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = db.Path, Pooling = false }.ToString());
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        return connection;
    }

    private sealed class CountMapper : IRowMapper<long>
    {
        public static readonly CountMapper Instance = new();

        public long Map(SqliteDataReader reader) => reader.GetInt64(0);
    }

    /// <summary>Cancels the read's own token while it maps a row, so the interrupt fires with the statement still open.</summary>
    private sealed class CancellingMapper(CancellationTokenSource cancel) : IRowMapper<long>
    {
        public long Map(SqliteDataReader reader)
        {
            cancel.Cancel();
            return reader.GetInt64(0);
        }
    }
}
