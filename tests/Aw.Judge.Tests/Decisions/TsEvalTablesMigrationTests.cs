using Aw.Judge.Infrastructure.Sqlite;
using Aw.Kernel.Data;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Judge.Tests.Decisions;

// #1280: the box's decisions.sqlite is at schema 3 and also holds the TS judge's eval_items / labels tables (a TS judge ran on
// the C# database after the cutover's 1-3). 0004 creates those names, so opening the store used to fail with
// "table eval_items already exists". The TS tables are renamed to ts_*, keeping their rows, before 0004 runs.
public sealed class TsEvalTablesMigrationTests : IDisposable
{
    private const string TsItems =
        "CREATE TABLE IF NOT EXISTS eval_items (id TEXT PRIMARY KEY, question TEXT NOT NULL, input_json TEXT NOT NULL, source TEXT NOT NULL UNIQUE, model_decision TEXT, created_at TEXT NOT NULL)";

    private const string TsItemsIndex = "CREATE INDEX IF NOT EXISTS eval_items_question ON eval_items(question, created_at)";

    private const string TsLabels =
        "CREATE TABLE IF NOT EXISTS labels (item_id TEXT NOT NULL, source TEXT NOT NULL, label TEXT NOT NULL, labeled_at TEXT NOT NULL, PRIMARY KEY (item_id, source))";

    private readonly TempStateDirectory _state = new();

    [Fact, Trait("Lane", "integration")]
    public async Task A_schema_3_database_holding_the_TS_eval_tables_migrates_and_keeps_the_TS_rows_aside()
    {
        var ct = TestContext.Current.CancellationToken;
        var (options, factory, all) = Open();
        await Migrate(factory, [.. all.Take(3)], ct);
        await Execute(factory, ct, TsItems, TsItemsIndex, TsLabels,
            "INSERT INTO eval_items (id, question, input_json, source, created_at) VALUES ('t1', 'ask-check', '{}', 'decision:t1', '2026-01-01T00:00:00.0000000Z')",
            "INSERT INTO labels (item_id, source, label, labeled_at) VALUES ('t1', 'human', 'ask', '2026-01-02T00:00:00.0000000Z')");

        var opened = await new JudgeDatabase(options, new FakeTimeProvider()).BeginRead(ct);

        Assert.True(opened.IsSuccess, opened.IsError ? opened.Error.Message : "");
        await opened.Value.DisposeAsync();
        Assert.Equal(all.Count, await Scalar(factory, "SELECT MAX(version) FROM schema_version", ct));
        Assert.Equal(1L, await Scalar(factory, "SELECT COUNT(*) FROM ts_labels WHERE source = 'human'", ct));
        Assert.Equal(1L, await Scalar(factory, "SELECT COUNT(*) FROM ts_eval_items WHERE id = 't1'", ct));
        Assert.Equal(0L, await Scalar(factory, "SELECT COUNT(*) FROM eval_items", ct));
        await Execute(factory, ct, "INSERT INTO eval_items (id, question, input_json, source, created_at) VALUES ('i1', 'ask-check', '{}', 'decision:d1', '2026-01-01T00:00:00.0000000Z')");
        _ = await Assert.ThrowsAsync<SqliteException>(() => Execute(factory, ct,
            "INSERT INTO labels (item_id, source, label, labeled_at) VALUES ('i1', 'bogus', 'ask', '2026-01-02T00:00:00.0000000Z')"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_schema_3_database_holding_only_the_TS_eval_items_migrates()
    {
        var ct = TestContext.Current.CancellationToken;
        var (options, factory, all) = Open();
        await Migrate(factory, [.. all.Take(3)], ct);
        await Execute(factory, ct, TsItems, TsItemsIndex);

        var opened = await new JudgeDatabase(options, new FakeTimeProvider()).BeginRead(ct);

        Assert.True(opened.IsSuccess, opened.IsError ? opened.Error.Message : "");
        await opened.Value.DisposeAsync();
        Assert.Equal(all.Count, await Scalar(factory, "SELECT MAX(version) FROM schema_version", ct));
        Assert.Equal(1L, await Scalar(factory, "SELECT COUNT(*) FROM sqlite_master WHERE name = 'ts_eval_items'", ct));
        Assert.Equal(0L, await Scalar(factory, "SELECT COUNT(*) FROM sqlite_master WHERE name = 'ts_labels'", ct));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_schema_4_database_is_left_alone()
    {
        var ct = TestContext.Current.CancellationToken;
        var (options, factory, all) = Open();
        await Migrate(factory, [.. all.Take(4)], ct);

        var opened = await new JudgeDatabase(options, new FakeTimeProvider()).BeginRead(ct);

        Assert.True(opened.IsSuccess, opened.IsError ? opened.Error.Message : "");
        await opened.Value.DisposeAsync();
        Assert.Equal(0L, await Scalar(factory, "SELECT COUNT(*) FROM sqlite_master WHERE name LIKE 'ts\\_%' ESCAPE '\\'", ct));
        Assert.Equal(1L, await Scalar(factory, "SELECT COUNT(*) FROM sqlite_master WHERE name = 'eval_items'", ct));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_TS_judge_s_open_time_DDL_on_a_migrated_database_changes_nothing_the_C_store_relies_on()
    {
        var ct = TestContext.Current.CancellationToken;
        var (options, factory, all) = Open();
        var first = await new JudgeDatabase(options, new FakeTimeProvider()).BeginRead(ct);
        Assert.True(first.IsSuccess, first.IsError ? first.Error.Message : "");
        await first.Value.DisposeAsync();

        await Execute(factory, ct, TsItems, TsItemsIndex, TsLabels);   // a TS judge still running opens the database like this

        var again = await new JudgeDatabase(options, new FakeTimeProvider()).BeginRead(ct);
        Assert.True(again.IsSuccess, again.IsError ? again.Error.Message : "");
        await again.Value.DisposeAsync();
        Assert.Equal(all.Count, await Scalar(factory, "SELECT MAX(version) FROM schema_version", ct));
        await Execute(factory, ct, "INSERT INTO eval_items (id, question, input_json, source, created_at) VALUES ('i1', 'ask-check', '{}', 'decision:d1', '2026-01-01T00:00:00.0000000Z')");
        _ = await Assert.ThrowsAsync<SqliteException>(() => Execute(factory, ct,
            "INSERT INTO labels (item_id, source, label, labeled_at) VALUES ('i1', 'bogus', 'ask', '2026-01-02T00:00:00.0000000Z')"));
    }

    public void Dispose() => _state.Dispose();

    private (SqliteOptions Options, SqliteConnectionFactory Factory, IReadOnlyList<Migration> All) Open()
    {
        var directory = Path.Combine(_state.Path, "judge");
        _ = Directory.CreateDirectory(directory);
        var options = JudgeDatabase.OptionsFor(directory);
        var migrations = EmbeddedMigrations.Load(typeof(JudgeDatabase).Assembly, JudgeDatabase.ResourcePrefix);
        Assert.True(migrations.IsSuccess);
        return (options, new SqliteConnectionFactory(options), migrations.Value);
    }

    private static async Task Migrate(SqliteConnectionFactory factory, IReadOnlyList<Migration> migrations, CancellationToken ct) =>
        Assert.True((await new MigrationRunner(factory, new FakeTimeProvider()).Migrate(migrations, ct)).IsSuccess);

    private static async Task Execute(SqliteConnectionFactory factory, CancellationToken ct, params string[] statements)
    {
        await using var connection = (await factory.Open(ct)).Value;
        foreach (var statement in statements)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = statement;
            _ = await command.ExecuteNonQueryAsync(ct);
        }
    }

    private static async Task<long> Scalar(SqliteConnectionFactory factory, string sql, CancellationToken ct)
    {
        await using var connection = (await factory.Open(ct)).Value;
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture);
    }
}
