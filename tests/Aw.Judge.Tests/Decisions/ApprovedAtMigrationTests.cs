using Aw.Judge.Core.Ids;
using Aw.Judge.Host;
using Aw.Judge.Infrastructure.Sqlite;
using Aw.Kernel.Data;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Judge.Tests.Decisions;

// 0002_approved_at on a copy of the 0001 schema: a row written before it reads back as not approved, and can be approved.
public sealed class ApprovedAtMigrationTests : IDisposable
{
    private readonly TempStateDirectory _state = new();

    [Fact, Trait("Lane", "integration")]
    public async Task A_decision_from_the_0001_schema_reads_back_unapproved_and_can_be_approved()
    {
        var ct = TestContext.Current.CancellationToken;
        var options = JudgeDatabase.OptionsFor(Path.Combine(_state.Path, "judge"));
        Directory.CreateDirectory(Path.Combine(_state.Path, "judge"));
        var migrations = EmbeddedMigrations.Load(typeof(JudgeDatabase).Assembly, JudgeDatabase.ResourcePrefix).Value;
        var factory = new SqliteConnectionFactory(options);
        Assert.True((await new MigrationRunner(factory, new FakeTimeProvider()).Migrate([migrations[0]], ct)).IsSuccess);
        await using (var connection = (await factory.Open(ct)).Value)
        {
            await using var insert = connection.CreateCommand();
            insert.CommandText =
                "INSERT INTO decisions (id, ts, question, content_class, provider, confidence, reason_code, latency_ms, input_digest) " +
                "VALUES ('old', '2026-01-01T00:00:00.0000000Z', 'wake-gate', 'general', 'rules', 1, 'pre-rule', 0, '0123456789abcdef')";
            _ = await insert.ExecuteNonQueryAsync(ct);
        }

        var log = new SqliteDecisionLog(new JudgeDatabase(options, new FakeTimeProvider()), AwJson.CreateOptions(JudgeJsonContext.Default));
        var before = await log.Find(DecisionId.From("old"), ct);
        _ = await log.MarkApproved(DecisionId.From("old"), KnownInstants.Epoch, ct);
        var after = await log.Find(DecisionId.From("old"), ct);

        Assert.Null(before.Value.ApprovedAt);
        Assert.Equal(KnownInstants.Epoch, after.Value.ApprovedAt);
    }

    public void Dispose() => _state.Dispose();
}
