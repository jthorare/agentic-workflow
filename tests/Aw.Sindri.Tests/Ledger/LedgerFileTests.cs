using System.Globalization;
using Aw.Kernel.Data;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Ledger;
using Aw.Sindri.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Ledger;

public sealed class LedgerFileTests : IDisposable
{
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    [Fact, Trait("Lane", "integration")]
    public async Task Each_migration_applies_on_the_previous_schema_and_the_outbox_table_is_pinned_at_version_3()
    {
        var path = Path.Combine(_home.Path, "steps.db");
        var runner = new MigrationRunner(new SqliteConnectionFactory(new SqliteOptions(path)), new FakeTimeProvider(), backupBeforeUpgrade: false);

        for (var version = 1; version <= LedgerDatabase.Migrations.Count; version++)
        {
            var step = await runner.Migrate(LedgerDatabase.Migrations, TestContext.Current.CancellationToken, targetVersion: version);
            Assert.True(step.IsSuccess, step.IsError ? step.Error.Message : null);
        }

        // S1-D9 (engine spec §7.2): S1's two files, then the outbox at 3, then Sindri's own files from 4 up.
        Assert.Equal(["1 events", "2 lease", "3 outbox_commands", "4 recipes", "5 temporal_event_id", "6 workflow_claims", "7 pr_comment_marks", "8 workflows", "9 workflow_runs", "10 connections", "11 memory", "12 retire_recipes", "13 poll_cursors", "14 workflow_seen", "15 resources", "16 memory_fact_written_by", "17 memory_fact_refreshed_by", "18 toolkits", "19 events_item_seq", "20 node_run_raw"],
            LedgerDatabase.Migrations.Select(m => string.Create(CultureInfo.InvariantCulture, $"{m.Version} {m.Name}")), StringComparer.Ordinal);
        Assert.Equal(SqliteOutboxSchema.CreateMigration(3).Checksum, LedgerDatabase.Migrations[2].Checksum);
        Assert.Equal(["connection", "events", "lease", "memory", "memory_fact", "outbox_commands", "poll_cursor", "resource", "toolkit", "toolkit_call", "workflow", "workflow_node_run", "workflow_revision", "workflow_run", "workflow_schedule_tick", "workflow_seen"], Tables(path));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_copy_of_the_previous_schema_upgrades_on_the_first_write_and_keeps_its_rows()
    {
        // The previous schema is S1's: events, lease and the outbox at version 3 (engine spec §11).
        var previous = Path.Combine(_home.Path, "previous.db");
        var runner = new MigrationRunner(new SqliteConnectionFactory(new SqliteOptions(previous)), new FakeTimeProvider(), backupBeforeUpgrade: false);
        Assert.True((await runner.Migrate(LedgerDatabase.Migrations, TestContext.Current.CancellationToken, targetVersion: 3)).IsSuccess);
        Execute(previous, "INSERT INTO lease (id, host, pid, pid_start, boot_id, epoch, acquired_at, expires_at) " +
            "VALUES (1, 'host-a', 42, '2026-10-10T07:05:07.0000000Z', 'boot-a', 7, '2026-10-10T07:05:07.0000000Z', '2026-10-10T07:05:37.0000000Z')");
        var home = Path.Combine(_home.Path, "home");
        _ = Directory.CreateDirectory(home);
        File.Copy(previous, Path.Combine(home, "ledger.db"));
        using var database = new LedgerDatabase(new SindriSettings(home, Path.Combine(home, "profile.yaml")), new FakeTimeProvider());

        Assert.True((await database.Writable(TestContext.Current.CancellationToken)).IsSuccess);

        Assert.True((await database.Schema(TestContext.Current.CancellationToken)).Value.IsCurrent);
        Assert.Equal("7", Scalar(Path.Combine(home, "ledger.db"), "SELECT epoch FROM lease WHERE id = 1"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Migration_0012_drops_the_recipe_tables_and_keeps_events_after_a_backup()
    {
        // Issue 2258 PR 3: the retiring migration drops the four recipe-era tables (0004, 0006, 0007); later migrations run after it.
        var retiring = Assert.Single(LedgerDatabase.Migrations, static m => m.Name == "retire_recipes");
        Assert.Equal(12, retiring.Version);

        var previous = Path.Combine(_home.Path, "previous.db");
        var runner = new MigrationRunner(new SqliteConnectionFactory(new SqliteOptions(previous)), new FakeTimeProvider(), backupBeforeUpgrade: false);
        Assert.True((await runner.Migrate(LedgerDatabase.Migrations, TestContext.Current.CancellationToken, targetVersion: retiring.Version - 1)).IsSuccess);
        const string at = "'2026-10-10T07:05:07.0000000Z'";
        Execute(previous, "INSERT INTO recipes (hash, id, version, text, registered_at) VALUES ('sha256:" + new string('a', 64) + "', 'workflow.step', 1, '{}', " + at + ")");
        Execute(previous, "INSERT INTO recipe_approvals (id, version, hash, approver, approved_at, via, blocks_json, children_json) " +
            "VALUES ('workflow.step', 1, 'sha256:" + new string('a', 64) + "', 'human:owner', " + at + ", 'desk', '{}', '{}')");
        Execute(previous, "INSERT INTO workflow_claims (item, owner, attempt, claimed_at) VALUES ('T-3', 'T-3/item/1', 1, " + at + ")");
        Execute(previous, "INSERT INTO pr_comment_marks (forge, repo, pr, comment_id, edited_at, instance, outcome, at, effect) " +
            "VALUES ('github', 'o/r', 7, 'c1', " + at + ", 'T-3/item/1', 'posted', " + at + ", 'e1')");
        Execute(previous, "INSERT INTO events (id, type, schema_version, occurred_at, actor, tenant, ring, correlation_id, epoch, idempotency_key, payload_json) " +
            "VALUES ('evt-1', 'workflow.started', 1, " + at + ", 'host:sindri', 't', 0, 'c-1', 1, 'k-1', '{}')");
        var home = Path.Combine(_home.Path, "home");
        _ = Directory.CreateDirectory(home);
        var ledger = Path.Combine(home, "ledger.db");
        File.Copy(previous, ledger);
        using var database = new LedgerDatabase(new SindriSettings(home, Path.Combine(home, "profile.yaml")), new FakeTimeProvider());

        Assert.True((await database.Writable(TestContext.Current.CancellationToken)).IsSuccess);

        Assert.Equal(["connection", "events", "lease", "memory", "memory_fact", "outbox_commands", "poll_cursor", "resource", "toolkit", "toolkit_call", "workflow", "workflow_node_run", "workflow_revision", "workflow_run", "workflow_schedule_tick", "workflow_seen"], Tables(ledger));
        Assert.Equal("evt-1", Scalar(ledger, "SELECT id FROM events"));
        Assert.True(File.Exists($"{ledger}.bak-v{(retiring.Version - 1).ToString(CultureInfo.InvariantCulture)}"));
    }

    [Fact, Trait("Lane", "integration")]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public async Task The_first_write_creates_an_owner_only_file_and_reads_never_create_one()
    {
        var home = Path.Combine(_home.Path, "home");
        var probe = new FakeProcessProbe();
        var stores = SqliteStores.Create(home, new FakeTimeProvider(), probe);

        Assert.Null((await stores.Lease.Read(TestContext.Current.CancellationToken)).Value);
        Assert.Equal(0, (await stores.Ledger.Schema(TestContext.Current.CancellationToken)).Value.Applied);
        Assert.False(File.Exists(Path.Combine(home, "ledger.db")));

        _ = (await stores.Lease.Acquire(probe.Self(), TestContext.Current.CancellationToken)).Value;

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(home));
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(Path.Combine(home, "ledger.db")));
        Assert.True((await stores.Ledger.Schema(TestContext.Current.CancellationToken)).Value.IsCurrent);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_instance_scan_walks_the_idempotency_key_index_not_the_event_log()
    {
        var path = Path.Combine(_home.Path, "plan.db");
        var runner = new MigrationRunner(new SqliteConnectionFactory(new SqliteOptions(path)), new FakeTimeProvider(), backupBeforeUpgrade: false);
        Assert.True((await runner.Migrate(LedgerDatabase.Migrations, TestContext.Current.CancellationToken)).IsSuccess);
        var statement = LedgerSql.ScanAfter(0, 500, new LedgerFilter(Types: null, Item: null, Instance: InstanceAddress.From("T-1/step/1")));

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

        Assert.True(
            plan.Exists(static step => step.StartsWith("SEARCH events USING INDEX sqlite_autoindex_events", StringComparison.Ordinal) && step.Contains("idempotency_key", StringComparison.Ordinal)),
            string.Join("; ", plan));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_newest_key_under_a_prefix_is_one_seek_on_the_idempotency_key_index()
    {
        var path = Path.Combine(_home.Path, "prefix-plan.db");
        var runner = new MigrationRunner(new SqliteConnectionFactory(new SqliteOptions(path)), new FakeTimeProvider(), backupBeforeUpgrade: false);
        Assert.True((await runner.Migrate(LedgerDatabase.Migrations, TestContext.Current.CancellationToken)).IsSuccess);

        using var connection = new SqliteConnection($"Data Source={path};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "EXPLAIN QUERY PLAN " + LedgerSql.SelectLatestByKeyPrefix;
        _ = command.Parameters.AddWithValue("$from", "connection.polled:a:k:");
        _ = command.Parameters.AddWithValue("$to", "connection.polled:a:k;");
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
        Assert.DoesNotContain(plan, static step => step.Contains("TEMP B-TREE", StringComparison.Ordinal));
        Assert.DoesNotContain(plan, static step => step.StartsWith("SCAN", StringComparison.Ordinal));
    }

    private static void Execute(string path, string sql)
    {
        using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        _ = command.ExecuteNonQuery();
    }

    private static string? Scalar(string path, string sql)
    {
        using var connection = new SqliteConnection($"Data Source={path};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static List<string> Tables(string path)
    {
        using var connection = new SqliteConnection($"Data Source={path};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT IN ('schema_version', 'sqlite_sequence') ORDER BY name";
        using var reader = command.ExecuteReader();
        var names = new List<string>();
        while (reader.Read())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }
}
