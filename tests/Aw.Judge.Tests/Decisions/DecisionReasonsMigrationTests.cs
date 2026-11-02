using Aw.Judge.Core.Ids;
using Aw.Judge.Host;
using Aw.Judge.Infrastructure.Sqlite;
using Aw.Kernel.Data;
using Aw.Kernel.Json;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Judge.Tests.Decisions;

// 0005_decision_reasons on a copy of the 0004 schema (#1248): a detail row written before it reads back with reasons
// "not recorded" (null), and the new column keeps what is written to it.
public sealed class DecisionReasonsMigrationTests : IDisposable
{
    private readonly TempStateDirectory _state = new();

    [Fact, Trait("Lane", "integration")]
    public async Task A_detail_row_from_the_0004_schema_reads_back_with_reasons_not_recorded()
    {
        var ct = TestContext.Current.CancellationToken;
        var directory = Path.Combine(_state.Path, "judge");
        _ = Directory.CreateDirectory(directory);
        var options = JudgeDatabase.OptionsFor(directory);
        var migrations = EmbeddedMigrations.Load(typeof(JudgeDatabase).Assembly, JudgeDatabase.ResourcePrefix).Value;
        var factory = new SqliteConnectionFactory(options);
        Assert.True((await new MigrationRunner(factory, new FakeTimeProvider()).Migrate([.. migrations.Take(4)], ct)).IsSuccess);
        await using (var connection = (await factory.Open(ct)).Value)
        {
            foreach (var sql in new[]
            {
                "INSERT INTO decisions (id, ts, question, content_class, provider, confidence, reason_code, latency_ms, input_digest) VALUES ('old', '2026-01-01T00:00:00.0000000Z', 'resolution-check', 'brief', 'claude-cli', 0.9, 'model', 0, '0123456789abcdef')",
                "INSERT INTO decision_details (id, input_json) VALUES ('old', '{}')",
            })
            {
                await using var command = connection.CreateCommand();
                command.CommandText = sql;
                _ = await command.ExecuteNonQueryAsync(ct);
            }
        }

        var log = new SqliteDecisionLog(new JudgeDatabase(options, new FakeTimeProvider()), AwJson.CreateOptions(JudgeJsonContext.Default));
        var found = await log.Find(DecisionId.From("old"), ct);

        Assert.True(found.IsSuccess);
        Assert.NotNull(found.Value.Details);
        Assert.Null(found.Value.Details.Reasons);
    }

    public void Dispose() => _state.Dispose();
}
