using System.Globalization;
using Aw.Kernel.Data;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Infrastructure.Ledger;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Ledger;

/// <summary>
/// #2774: an instance-filtered ledger read with type globs must walk the idempotency_key range, not events_type_seq over every
/// row of those types in the ledger. The real migrations, seeded rows, no ANALYZE (a box never runs it).
/// </summary>
public sealed class LedgerInstanceScanPlanTests : IDisposable
{
    private static readonly InstanceAddress _instance = InstanceAddress.From("ITM-1/item/1");

    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    [Theory, Trait("Lane", "integration")]
    [InlineData("workflow.instance.started")]
    [InlineData("workflow.*")]
    [InlineData("workflow.instance.started,workflow.node.completed")]
    [InlineData("workflow.instance.started,workflow.*")]
    public async Task Instance_filtered_scan_with_types_uses_the_key_range(string types)
    {
        var path = Path.Combine(_home.Path, "plan.db");
        var runner = new MigrationRunner(new SqliteConnectionFactory(new SqliteOptions(path)), new FakeTimeProvider(), backupBeforeUpgrade: false);
        Assert.True((await runner.Migrate(LedgerDatabase.Migrations, TestContext.Current.CancellationToken)).IsSuccess);
        Seed(path);
        var filter = new LedgerFilter(TypeFilter.Parse(types).Value, Item: null, Instance: _instance);
        var statement = LedgerSql.ScanAfter(0, 500, filter);

        using var connection = new SqliteConnection($"Data Source={path};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "EXPLAIN QUERY PLAN " + statement.Sql;
        statement.Bind?.Invoke(command);
        using var reader = command.ExecuteReader();
        var plan = new List<string>();
        while (reader.Read())
        {
            plan.Add(reader.GetString(3));
        }

        var shown = string.Join("; ", plan);
        Assert.True(
            plan.Exists(static step => step.StartsWith("SEARCH events USING INDEX sqlite_autoindex_events", StringComparison.Ordinal) && step.Contains("idempotency_key", StringComparison.Ordinal)),
            shown);
        // why not "no TEMP B-TREE": the few rows the key range finds are sorted by seq on purpose (LedgerSql.ScanAfter), a temp B-tree over them is fine; one over every row of the type is not
        Assert.DoesNotContain(plan, static step => step.Contains("events_type_seq", StringComparison.Ordinal));
    }

    private static void Seed(string path)
    {
        string[] types = ["workflow.instance.started", "workflow.node.completed", "agent.launched", "item.created", "connection.polled"];
        using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        connection.Open();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = LedgerSql.InsertEvent;
        for (var i = 0; i < 5_000; i++)
        {
            var n = i.ToString(CultureInfo.InvariantCulture);
            var key = i % 50 == 0 ? $"workflow:{_instance.Value}:{n}" : i % 7 == 0 ? $"workflow:ITM-{n}/item/1:{n}" : $"other:{n}";
            command.Parameters.Clear();
            _ = command.Parameters.AddWithValue("$id", "id-" + n);
            _ = command.Parameters.AddWithValue("$type", types[i % types.Length]);
            _ = command.Parameters.AddWithValue("$schemaVersion", 1);
            _ = command.Parameters.AddWithValue("$occurredAt", "2026-01-01T00:00:00.0000000+00:00");
            _ = command.Parameters.AddWithValue("$actor", "test");
            _ = command.Parameters.AddWithValue("$tenant", "t");
            _ = command.Parameters.AddWithValue("$ring", 0);
            _ = command.Parameters.AddWithValue("$item", DBNull.Value);
            _ = command.Parameters.AddWithValue("$correlationId", "c");
            _ = command.Parameters.AddWithValue("$causationId", DBNull.Value);
            _ = command.Parameters.AddWithValue("$traceId", DBNull.Value);
            _ = command.Parameters.AddWithValue("$epoch", 1);
            _ = command.Parameters.AddWithValue("$key", key);
            _ = command.Parameters.AddWithValue("$payload", "{}");
            _ = command.Parameters.AddWithValue("$temporalEventId", DBNull.Value);
            _ = command.ExecuteScalar();
        }

        transaction.Commit();
    }
}
