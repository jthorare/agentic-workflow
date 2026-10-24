using Aw.Judge.Infrastructure.Sqlite;
using Aw.Kernel.Data;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Judge.Tests.Decisions;

// 0003_prompt_sort on a copy of the 0002 schema (cutover unit 6.0): the rows written before it are kept, the sorter's two
// tables arrive, and each axis and run belongs to a recorded decision.
public sealed class PromptSortMigrationTests : IDisposable
{
    private readonly TempStateDirectory _state = new();

    [Fact, Trait("Lane", "integration")]
    public async Task A_database_at_schema_2_migrates_to_3_and_keeps_its_rows()
    {
        var ct = TestContext.Current.CancellationToken;
        var (factory, all) = Open();
        Assert.True((await new MigrationRunner(factory, new FakeTimeProvider()).Migrate([.. all.Take(2)], ct)).IsSuccess);
        await Execute(factory, ct,
            "INSERT INTO decisions (id, ts, question, content_class, provider, confidence, reason_code, latency_ms, input_digest, approved_at) VALUES ('d1', '2026-01-01T00:00:00.0000000Z', 'wake-gate', 'general', 'rules', 1, 'pre-rule', 0, '0123456789abcdef', '2026-01-02T00:00:00.0000000Z')",
            "INSERT INTO briefs (tool_use_id, session_id, prompt_id, subagent_type, goal, acceptance_criteria, proof_command, saved_at) VALUES ('toolu_1', 's1', 'p1', 'general-purpose', 'g', 'a', 'c', '2026-01-01T00:00:00.0000000Z')");
        Assert.Equal(2L, await Scalar(factory, "SELECT MAX(version) FROM schema_version", ct));

        Assert.True((await new MigrationRunner(factory, new FakeTimeProvider()).Migrate(all, ct)).IsSuccess);

        Assert.Equal(3L, await Scalar(factory, "SELECT MAX(version) FROM schema_version", ct));
        Assert.Equal(1L, await Scalar(factory, "SELECT COUNT(*) FROM decisions WHERE id = 'd1' AND approved_at IS NOT NULL", ct));
        Assert.Equal(1L, await Scalar(factory, "SELECT COUNT(*) FROM briefs WHERE tool_use_id = 'toolu_1'", ct));
        await Execute(factory, ct,
            "INSERT INTO prompt_sort_axes (decision_id, axis, value, heuristic, source, status, probability, confidence) VALUES ('d1', 'complexity', 'M', 'S', 'blend', 'overrode', 0.9, 0.8)",
            "INSERT INTO prompt_sort_runs (decision_id, mode, reason) VALUES ('d1', 'blend', 'sorted')");
        Assert.Equal("[]", await Text(factory, "SELECT would_fire FROM prompt_sort_runs WHERE decision_id = 'd1'", ct));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task An_axis_or_run_without_a_recorded_decision_is_refused_and_an_axis_is_one_per_decision()
    {
        var ct = TestContext.Current.CancellationToken;
        var (factory, all) = Open();
        Assert.True((await new MigrationRunner(factory, new FakeTimeProvider()).Migrate(all, ct)).IsSuccess);
        await Execute(factory, ct, "INSERT INTO decisions (id, ts, question, content_class, provider, confidence, reason_code, latency_ms, input_digest) VALUES ('d1', '2026-01-01T00:00:00.0000000Z', 'prompt-sort', 'brief', 'rules', 0, 'sorted', 0, '0123456789abcdef')");
        const string axis = "INSERT INTO prompt_sort_axes (decision_id, axis, value, heuristic, source, status) VALUES ('{0}', 'complexity', 'M', 'S', 'blend', 'agreed')";

        _ = await Assert.ThrowsAsync<SqliteException>(() => Execute(factory, ct, string.Format(System.Globalization.CultureInfo.InvariantCulture, axis, "nobody")));
        await Execute(factory, ct, string.Format(System.Globalization.CultureInfo.InvariantCulture, axis, "d1"));
        _ = await Assert.ThrowsAsync<SqliteException>(() => Execute(factory, ct, string.Format(System.Globalization.CultureInfo.InvariantCulture, axis, "d1")));
        _ = await Assert.ThrowsAsync<SqliteException>(() => Execute(factory, ct, "INSERT INTO prompt_sort_runs (decision_id, mode, reason) VALUES ('nobody', 'blend', 'sorted')"));
    }

    public void Dispose() => _state.Dispose();

    private (SqliteConnectionFactory Factory, IReadOnlyList<Migration> All) Open()
    {
        _ = Directory.CreateDirectory(Path.Combine(_state.Path, "judge"));
        var factory = new SqliteConnectionFactory(JudgeDatabase.OptionsFor(Path.Combine(_state.Path, "judge")));
        var migrations = EmbeddedMigrations.Load(typeof(JudgeDatabase).Assembly, JudgeDatabase.ResourcePrefix);
        Assert.True(migrations.IsSuccess);
        Assert.Equal([1, 2, 3], [.. migrations.Value.Select(m => m.Version)]);
        return (factory, migrations.Value);
    }

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

    private static async Task<string> Text(SqliteConnectionFactory factory, string sql, CancellationToken ct)
    {
        await using var connection = (await factory.Open(ct)).Value;
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(await command.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture) ?? "";
    }
}
