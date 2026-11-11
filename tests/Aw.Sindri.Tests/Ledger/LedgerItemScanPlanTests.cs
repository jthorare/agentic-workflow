using Aw.Kernel.Data;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Infrastructure.Ledger;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Ledger;

/// <summary>#2773: a read filtered by item must seek an index on the item, not walk the whole event log by sequence.</summary>
public sealed class LedgerItemScanPlanTests : IDisposable
{
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    [Fact, Trait("Lane", "integration")]
    public async Task Item_filtered_scan_uses_the_item_index()
    {
        var path = Path.Combine(_home.Path, "item-plan.db");
        var runner = new MigrationRunner(new SqliteConnectionFactory(new SqliteOptions(path)), new FakeTimeProvider(), backupBeforeUpgrade: false);
        Assert.True((await runner.Migrate(LedgerDatabase.Migrations, TestContext.Current.CancellationToken)).IsSuccess);
        var statement = LedgerSql.ScanAfter(0, 500, new LedgerFilter(Types: null, Item: ItemId.From("ITM-" + new string('a', 32))));

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
            plan.Exists(static step => step.StartsWith("SEARCH events USING", StringComparison.Ordinal) && step.Contains("events_item_seq", StringComparison.Ordinal)),
            shown);
        Assert.DoesNotContain(plan, static step => step.Contains("TEMP B-TREE", StringComparison.Ordinal));
    }
}
