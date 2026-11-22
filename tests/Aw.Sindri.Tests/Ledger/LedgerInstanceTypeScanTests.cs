using Aw.Kernel.Data;
using Aw.Kernel.Events;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Ledger;
using Aw.Sindri.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Ledger;

/// <summary>
/// #2774: a read filtered by instance and type must seek the instance's key range on the idempotency_key index, never 'events_type_seq' (which walks
/// every row of those types in the whole ledger). The ORDER BY seq still sorts, but only the instance's own rows. Without an instance the type index stays.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class LedgerInstanceTypeScanTests : IDisposable
{
    private static readonly InstanceAddress _instance = InstanceAddress.From("T-1/step/1");
    private static readonly TypeFilter _engineRows = TypeFilter.Parse("workflow.started,workflow.transition,workflow.refused,workflow.notified").Value;
    private readonly TempHome _home = new();
    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch);
    private readonly SequentialIdGenerator _ids = new();
    private readonly FakeProcessProbe _probe = new();
    private LedgerDatabase? _database;
    private SindriStores? _stores;
    private LeaseGrant? _grant;

    public void Dispose()
    {
        _database?.Dispose();
        _home.Dispose();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_instance_and_type_scan_seeks_the_idempotency_key_range_not_the_type_index()
    {
        var plan = await Plan(LedgerSql.ScanAfter(0, 500, new LedgerFilter(_engineRows, Item: null, Instance: _instance)));

        var shown = string.Join("; ", plan);
        Assert.True(
            plan.Exists(static step => step.StartsWith("SEARCH events USING INDEX sqlite_autoindex_events", StringComparison.Ordinal) && step.Contains("idempotency_key", StringComparison.Ordinal)),
            shown);
        Assert.DoesNotContain(plan, static step => step.Contains("events_type_seq", StringComparison.Ordinal));
    }

    [Fact]
    public async Task An_instance_scan_with_a_prefix_glob_seeks_the_idempotency_key_range()
    {
        var types = TypeFilter.Parse("workflow.started,workflow.node.*").Value;
        var plan = await Plan(LedgerSql.ScanAfter(0, 500, new LedgerFilter(types, Item: null, Instance: _instance)));

        Assert.True(plan.Exists(static step => step.Contains("idempotency_key", StringComparison.Ordinal)), string.Join("; ", plan));
        Assert.DoesNotContain(plan, static step => step.Contains("events_type_seq", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_type_scan_without_an_instance_still_uses_the_type_index()
    {
        var plan = await Plan(LedgerSql.ScanAfter(0, 500, new LedgerFilter(_engineRows, Item: null)));

        Assert.True(
            plan.Exists(static step => step.StartsWith("SEARCH events USING", StringComparison.Ordinal) && step.Contains("events_type_seq", StringComparison.Ordinal)),
            string.Join("; ", plan));
    }

    [Fact]
    public async Task An_instance_and_type_scan_keeps_that_instances_rows_of_those_types_oldest_first()
    {
        var started = await Append("workflow.started", "workflow:T-1/step/1:run-1:1");
        _ = await Append("workflow.started", "workflow:T-2/step/1:run-1:1");        // another instance, same type
        _ = await Append("workflow.started", "workflow:T-1/step/10:run-1:1");       // a longer address that shares the prefix
        _ = await Append("workflow.step", "workflow:T-1/step/1:run-1:2");           // this instance, a type the filter drops
        var moved = await Append("workflow.transition", "workflow:T-1/step/1:run-1:3");
        _ = await Append("workflow.transition", "other:T-1/step/1:run-1:3");         // not a mirrored row
        var refused = await Append("workflow.refused", "workflow:T-1/step/1:run-2:1");
        var filter = new LedgerFilter(_engineRows, Item: null, Instance: _instance);

        _database = new LedgerDatabase(new SindriSettings(_home.Path, Path.Combine(_home.Path, "profile.yaml")), _clock);
        var reader = new SqliteLedgerReader(_database);
        var scan = (await reader.Scan(0, 500, filter, Token)).Value;
        var everything = (await reader.Scan(0, 500, LedgerFilter.All, Token)).Value;

        Assert.Equal([started, moved, refused], scan.Events.Select(static e => e.Sequence));
        Assert.Equal(everything.Events.Where(filter.Keeps).Select(static e => e.Sequence), scan.Events.Select(static e => e.Sequence));
    }

    private async Task<long> Append(string type, string key)
    {
        _stores ??= SqliteStores.Create(_home.Path, _clock, _probe);
        var grant = _grant ??= (await _stores.Lease.Acquire(_probe.Self(), Token)).Value;
        var context = EventContext.Create("host:sindri", "local", 0, _ids.NewId()).Value;
        _clock.Advance(TimeSpan.FromMilliseconds(1));
        return (await _stores.Ledger.Append(new LedgerEntry(context.Envelope(type, """{"instance":"T-1/step/1"}""", _clock, _ids), key, Item: null), grant, Token)).Value.Sequence;
    }

    private async Task<List<string>> Plan(SqlStatement statement)
    {
        var path = Path.Combine(_home.Path, "instance-type-plan.db");
        var runner = new MigrationRunner(new SqliteConnectionFactory(new SqliteOptions(path)), new FakeTimeProvider(), backupBeforeUpgrade: false);
        Assert.True((await runner.Migrate(LedgerDatabase.Migrations, Token)).IsSuccess);

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

        return plan;
    }
}
