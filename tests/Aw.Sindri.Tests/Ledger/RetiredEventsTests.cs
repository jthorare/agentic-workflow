using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Ledger;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Ledger;

public sealed class RetiredEventsTests : IDisposable
{
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    [Theory, Trait("Lane", "integration")]
    [InlineData("workflow.started", """{"instance":"T-1/item/1","from":"","to":"produce","trigger":"start","outcome":null,"run":null}""")]
    [InlineData("workflow.transition", """{"instance":"T-1/item/1","from":"produce","to":"checks","trigger":"step.completed","outcome":"done","run":"1.1"}""")]
    [InlineData("workflow.transition", """{"instance":"T-1/item/1","from":"checks","to":"cancelled","trigger":"cancel","outcome":null,"run":null}""")]
    [InlineData("recipe.approved", """{"id":"workflow.step","version":1,"hash":"sha256:aaaa"}""")]
    [InlineData("line.phaseChanged", """{"line":"L-1","phase":"build","to":"review"}""")]
    [InlineData("scope.frozen", """{"packet":"P-1","hash":"sha256:bbbb"}""")]
    public async Task Retired_workflow_and_line_events_still_deserialize_as_history(string type, string payload)
    {
        var home = _home.Path;
        using var database = new LedgerDatabase(new SindriSettings(home, Path.Combine(home, "profile.yaml")), new FakeTimeProvider());
        Assert.True((await database.Writable(TestContext.Current.CancellationToken)).IsSuccess);
        using (var connection = new SqliteConnection($"Data Source={Path.Combine(home, "ledger.db")}"))
        {
            connection.Open();
            using var insert = connection.CreateCommand();
            insert.CommandText = "INSERT INTO events (id, type, schema_version, occurred_at, actor, tenant, ring, item, correlation_id, epoch, idempotency_key, payload_json) " +
                "VALUES ('evt-1', $type, 1, '2026-10-10T07:05:07.0000000Z', 'host:sindri', 't', 0, 'T-1', 'c-1', 1, 'k-1', $payload)";
            _ = insert.Parameters.AddWithValue("$type", type);
            _ = insert.Parameters.AddWithValue("$payload", payload);
            _ = insert.ExecuteNonQuery();
        }

        var read = await new SqliteLedgerReader(database).ReadAfter(0, 10, TestContext.Current.CancellationToken);

        var recorded = Assert.Single(read.Value);
        Assert.Equal(type, recorded.Envelope.Type);
        Assert.Equal(payload, recorded.Envelope.Payload);
    }
}
