using System.Globalization;
using Aw.Kernel.Data;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Ledger;
using Aw.Sindri.Infrastructure.Workflow;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow.Registry;
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
        Assert.Equal(["1 events", "2 lease", "3 outbox_commands", "4 recipes", "5 temporal_event_id", "6 workflow_claims", "7 pr_comment_marks"],
            LedgerDatabase.Migrations.Select(m => string.Create(CultureInfo.InvariantCulture, $"{m.Version} {m.Name}")), StringComparer.Ordinal);
        Assert.Equal(SqliteOutboxSchema.CreateMigration(3).Checksum, LedgerDatabase.Migrations[2].Checksum);
        Assert.Equal(["events", "lease", "outbox_commands", "recipe_approvals", "pr_comment_marks", "recipes", "workflow_claims"], Tables(path));
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
        var store = new SqliteRecipeStore(database);
        var recipe = RegistryData.Recipe("workflow.step");

        Assert.Empty((await store.All(TestContext.Current.CancellationToken)).Value);
        Assert.True((await store.Add(recipe, TestContext.Current.CancellationToken)).Value.Added);

        Assert.True((await database.Schema(TestContext.Current.CancellationToken)).Value.IsCurrent);
        Assert.Equal(recipe, (await store.Find(recipe.Hash, TestContext.Current.CancellationToken)).Value);
        Assert.Equal("7", Scalar(Path.Combine(home, "ledger.db"), "SELECT epoch FROM lease WHERE id = 1"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_copy_of_the_recipes_schema_gains_workflow_claims_on_the_first_take_and_keeps_its_recipes()
    {
        // The previous schema is main's: S1's three, the recipe registry at 4, the Temporal event id at 5; engine P6 unit (d) adds claims at 6.
        var previous = Path.Combine(_home.Path, "previous.db");
        var runner = new MigrationRunner(new SqliteConnectionFactory(new SqliteOptions(previous)), new FakeTimeProvider(), backupBeforeUpgrade: false);
        Assert.True((await runner.Migrate(LedgerDatabase.Migrations, TestContext.Current.CancellationToken, targetVersion: LedgerDatabase.ClaimsVersion - 1)).IsSuccess);
        Execute(previous, "INSERT INTO recipes (hash, id, version, text, registered_at) VALUES ('sha256:" + new string('a', 64) + "', 'workflow.step', 1, '{}', '2026-10-10T07:05:07.0000000Z')");
        var home = Path.Combine(_home.Path, "home");
        _ = Directory.CreateDirectory(home);
        File.Copy(previous, Path.Combine(home, "ledger.db"));
        using var database = new LedgerDatabase(new SindriSettings(home, Path.Combine(home, "profile.yaml")), new FakeTimeProvider());
        var claims = new SqliteWorkflowClaims(database, new FakeTimeProvider());
        var claim = new WorkflowClaim(ItemKey.From("T-3"), InstanceAddress.From("T-3/item/1"), 1);

        Assert.Null((await claims.Find(ItemKey.From("T-3"), TestContext.Current.CancellationToken)).Value);   // a read doesn't migrate
        Assert.Equal(claim, (await claims.Take(claim, ended: null, TestContext.Current.CancellationToken)).Value);

        Assert.True((await database.Schema(TestContext.Current.CancellationToken)).Value.IsCurrent);
        Assert.Equal(claim, (await claims.Find(ItemKey.From("T-3"), TestContext.Current.CancellationToken)).Value);
        Assert.Equal("workflow.step", Scalar(Path.Combine(home, "ledger.db"), "SELECT id FROM recipes"));
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
