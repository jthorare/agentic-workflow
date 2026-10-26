using Aw.Judge.Infrastructure.Sqlite;
using Aw.Kernel.Data;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Judge.Tests.Decisions;

// 0004_eval_labels on a copy of the 0003 schema (cutover unit 6.3): the rows written before it are kept, the eval tables
// arrive, an item is imported once per source and a label belongs to an item and has one of the three sources.
public sealed class EvalLabelsMigrationTests : IDisposable
{
    private readonly TempStateDirectory _state = new();

    [Fact, Trait("Lane", "integration")]
    public async Task A_database_at_schema_3_migrates_to_4_and_keeps_its_rows()
    {
        var ct = TestContext.Current.CancellationToken;
        var (factory, all) = Open();
        Assert.True((await new MigrationRunner(factory, new FakeTimeProvider()).Migrate([.. all.Take(3)], ct)).IsSuccess);
        await Execute(factory, ct,
            "INSERT INTO decisions (id, ts, question, content_class, provider, confidence, reason_code, latency_ms, input_digest) VALUES ('d1', '2026-01-01T00:00:00.0000000Z', 'prompt-sort', 'brief', 'rules', 0, 'sorted', 0, '0123456789abcdef')",
            "INSERT INTO prompt_sort_runs (decision_id, mode, reason) VALUES ('d1', 'blend', 'sorted')",
            "INSERT INTO briefs (tool_use_id, session_id, prompt_id, subagent_type, goal, acceptance_criteria, proof_command, saved_at) VALUES ('toolu_1', 's1', 'p1', 'general-purpose', 'g', 'a', 'c', '2026-01-01T00:00:00.0000000Z')");
        Assert.Equal(3L, await Scalar(factory, "SELECT MAX(version) FROM schema_version", ct));

        Assert.True((await new MigrationRunner(factory, new FakeTimeProvider()).Migrate([.. all.Take(4)], ct)).IsSuccess);

        Assert.Equal(4L, await Scalar(factory, "SELECT MAX(version) FROM schema_version", ct));
        Assert.Equal(1L, await Scalar(factory, "SELECT COUNT(*) FROM decisions WHERE id = 'd1'", ct));
        Assert.Equal(1L, await Scalar(factory, "SELECT COUNT(*) FROM prompt_sort_runs WHERE decision_id = 'd1'", ct));
        Assert.Equal(1L, await Scalar(factory, "SELECT COUNT(*) FROM briefs WHERE tool_use_id = 'toolu_1'", ct));
        Assert.Equal(0L, await Scalar(factory, "SELECT COUNT(*) FROM eval_items", ct));
        Assert.Equal(0L, await Scalar(factory, "SELECT COUNT(*) FROM labels", ct));
        await Execute(factory, ct,
            "INSERT INTO eval_items (id, question, input_json, source, model_decision, created_at) VALUES ('i1', 'ask-check', '{}', 'decision:d1', 'continue', '2026-01-01T00:00:00.0000000Z')",
            "INSERT INTO labels (item_id, source, label, labeled_at) VALUES ('i1', 'outcome', 'continue', '2026-01-02T00:00:00.0000000Z')");
        Assert.Equal(1L, await Scalar(factory, "SELECT COUNT(*) FROM labels WHERE item_id = 'i1'", ct));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task An_item_is_one_per_source_and_a_label_is_one_per_item_and_source_of_a_known_item()
    {
        var ct = TestContext.Current.CancellationToken;
        var (factory, all) = Open();
        Assert.True((await new MigrationRunner(factory, new FakeTimeProvider()).Migrate(all, ct)).IsSuccess);
        const string item = "INSERT INTO eval_items (id, question, input_json, source, created_at) VALUES ('{0}', 'ask-check', '{{}}', '{1}', '2026-01-01T00:00:00.0000000Z')";
        const string label = "INSERT INTO labels (item_id, source, label, labeled_at) VALUES ('{0}', '{1}', 'ask', '2026-01-02T00:00:00.0000000Z')";

        await Execute(factory, ct, Format(item, "i1", "decision:d1"));
        _ = await Assert.ThrowsAsync<SqliteException>(() => Execute(factory, ct, Format(item, "i2", "decision:d1")));
        await Execute(factory, ct, Format(label, "i1", "outcome"), Format(label, "i1", "adjudicator"));
        _ = await Assert.ThrowsAsync<SqliteException>(() => Execute(factory, ct, Format(label, "i1", "outcome")));
        _ = await Assert.ThrowsAsync<SqliteException>(() => Execute(factory, ct, Format(label, "nobody", "outcome")));
        _ = await Assert.ThrowsAsync<SqliteException>(() => Execute(factory, ct, Format(label, "i1", "guess")));
    }

    public void Dispose() => _state.Dispose();

    private static string Format(string template, string first, string second) =>
        string.Format(System.Globalization.CultureInfo.InvariantCulture, template, first, second);

    private (SqliteConnectionFactory Factory, IReadOnlyList<Migration> All) Open()
    {
        _ = Directory.CreateDirectory(Path.Combine(_state.Path, "judge"));
        var factory = new SqliteConnectionFactory(JudgeDatabase.OptionsFor(Path.Combine(_state.Path, "judge")));
        var migrations = EmbeddedMigrations.Load(typeof(JudgeDatabase).Assembly, JudgeDatabase.ResourcePrefix);
        Assert.True(migrations.IsSuccess);
        Assert.Equal([1, 2, 3, 4, 5], [.. migrations.Value.Select(m => m.Version)]);
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
}
