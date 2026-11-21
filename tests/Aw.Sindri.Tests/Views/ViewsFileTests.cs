using Aw.Kernel.Data;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Infrastructure.Views;
using Aw.Sindri.Tests.Ledger;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Views;

public sealed class ViewsFileTests : IDisposable
{
    private readonly TempHome _home = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _home.Dispose();

    [Fact, Trait("Lane", "integration")]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public async Task Views_db_is_0600_and_reads_never_create_it()
    {
        using var views = new SqliteViews(Path.Combine(_home.Path, "home"));

        Assert.Null((await views.Reader.State(Token)).Value);
        Assert.Empty((await views.Reader.List<ServiceRow>(ViewNames.Services, Token)).Value);
        Assert.False(File.Exists(views.Path));
        _ = (await views.Store.Open(ViewSchema.Version, Token)).Value;

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(views.Path));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_views_db_the_projector_created_but_hasnt_migrated_yet_reads_as_empty()
    {
        using var views = new SqliteViews(Path.Combine(_home.Path, "home"));
        _ = Directory.CreateDirectory(Path.GetDirectoryName(views.Path)!);
        await File.WriteAllBytesAsync(views.Path, [], Token);   // the window between the projector creating the file and its migration committing

        var state = await views.Reader.State(Token);
        var rows = await views.Reader.List<ServiceRow>(ViewNames.Services, Token);
        var row = await views.Reader.Get<ServiceRow>(ViewNames.Services, "any", Token);
        var range = await views.Reader.Range<TimelineRow>(ViewNames.ItemTimeline, "", beforeKey: null, 10, Token);

        Assert.Empty(range.Value);
        Assert.Null(state.Value);
        Assert.Empty(rows.Value);
        Assert.Null(row.Value);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Read_on_a_half_migrated_views_db_is_empty()
    {
        using var views = new SqliteViews(Path.Combine(_home.Path, "home"));
        _ = Directory.CreateDirectory(Path.GetDirectoryName(views.Path)!);
        var runner = new MigrationRunner(new SqliteConnectionFactory(new SqliteOptions(views.Path)), new FakeTimeProvider(), backupBeforeUpgrade: false);
        var halfway = await runner.Migrate(ViewsDatabase.Migrations, Token, targetVersion: 14);   // #2881: view_launches exists (migration 5), its 'reason' column (migration 15) does not

        var idle = await views.Reader.Where<LaunchRow>(ViewNames.Launches, ViewColumn.Reason, "idle", Token);
        var state = await views.Reader.State(Token);

        Assert.True(halfway.IsSuccess, halfway.IsError ? halfway.Error.Message : null);
        Assert.True(idle.IsSuccess, idle.IsError ? idle.Error.Message : null);
        Assert.Empty(idle.Value);
        Assert.True(state.IsSuccess, state.IsError ? state.Error.Message : null);
        Assert.Null(state.Value);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_views_migration_creates_the_state_and_one_table_per_view()
    {
        var path = Path.Combine(_home.Path, "steps.db");
        var runner = new MigrationRunner(new SqliteConnectionFactory(new SqliteOptions(path)), new FakeTimeProvider(), backupBeforeUpgrade: false);

        var migrated = await runner.Migrate(ViewsDatabase.Migrations, Token);

        Assert.True(migrated.IsSuccess, migrated.IsError ? migrated.Error.Message : null);
        Assert.Equal(["projector_state", "view_code_index", "view_decisions", "view_home_links", "view_home_rows", "view_item_aliases", "view_item_links", "view_item_timeline", "view_items", "view_jobs", "view_launches", "view_model_history", "view_model_runs", "view_needs_you", "view_profile", "view_project_attached", "view_project_hidden", "view_requests", "view_services", "view_todo", "view_workflow_runs"], Tables(path));
        Assert.Equal(["key", "row_json", "seq", "owner", "state"], Columns(path, "view_items"));
        Assert.Equal(["key", "row_json", "seq", "item", "state", "repo"], Columns(path, "view_requests"));
        Assert.Equal(["key", "row_json", "seq", "item", "state", "repo"], Columns(path, "view_decisions"));
        Assert.Equal(["key", "row_json", "seq", "item", "repo", "kind"], Columns(path, "view_needs_you"));
        Assert.Equal(["key", "row_json", "seq", "state", "kind", "repo"], Columns(path, "view_jobs"));
        Assert.Equal(["key", "row_json", "seq", "item", "state", "repo", "run_id", "node_id", "reason", "instance"], Columns(path, "view_launches"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Migration_0017_creates_home_tables()
    {
        var path = Path.Combine(_home.Path, "home.db");
        var runner = new MigrationRunner(new SqliteConnectionFactory(new SqliteOptions(path)), new FakeTimeProvider(), backupBeforeUpgrade: false);

        var migrated = await runner.Migrate(ViewsDatabase.Migrations, Token);

        Assert.True(migrated.IsSuccess, migrated.IsError ? migrated.Error.Message : null);
        Assert.Equal(["key", "row_json", "seq", "kind", "grp", "state", "since", "snooze_until", "done_at"], Columns(path, "view_home_rows"));
        Assert.Equal(["key", "row_json", "seq", "item", "to_id", "edge"], Columns(path, "view_home_links"));
        Assert.Equal(17, ViewsDatabase.Migrations.Count);   // no gaps (AW-DATA-005): 0017 follows 0016_items_state
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_view_name_has_a_table()
    {
        Assert.Equal(["code-index", "decisions", "home-links", "home-rows", "item-aliases", "item-links", "item-timeline", "items", "jobs", "launches", "model-history", "model-runs", "needs_you", "profile", "project-attached", "project-hidden", "requests", "services", "todo", "workflow-runs"], ViewNames.All.Order(StringComparer.Ordinal).ToList());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_views_db_at_schema_version_2_gains_the_request_tables_and_is_rebuilt()
    {
        var path = Path.Combine(_home.Path, "views.db");
        var runner = new MigrationRunner(new SqliteConnectionFactory(new SqliteOptions(path)), new FakeTimeProvider(), backupBeforeUpgrade: false);
        Assert.True((await runner.Migrate([.. ViewsDatabase.Migrations.Take(2)], Token)).IsSuccess);
        Exec(path, "INSERT INTO projector_state (id, cursor, schema_version, rebuilding) VALUES (1, 9, 2, 0)");
        Exec(path, "INSERT INTO view_items (key, row_json, seq) VALUES ('ITM-1', '{}', 4)");
        Assert.DoesNotContain("view_requests", Tables(path), StringComparer.Ordinal);

        using var views = new SqliteViews(_home.Path);
        var opened = (await views.Store.Open(ViewSchema.Version, Token)).Value;

        Assert.Equal(2, opened.SchemaVersion);   // the file's own version: the projector sees it differs and resets
        Assert.Contains("view_requests", Tables(path), StringComparer.Ordinal);
        Assert.Contains("view_decisions", Tables(path), StringComparer.Ordinal);
        Assert.Contains("view_needs_you", Tables(path), StringComparer.Ordinal);
        Assert.Equal(1, Count(path, "view_items"));   // the migration only adds tables
        var reset = (await views.Store.Reset(ViewNames.All, ViewSchema.Version, new FakeTimeProvider().GetUtcNow(), 0, Token)).Value;
        Assert.True(reset is { Cursor: 0, Rebuilding: true, SchemaVersion: ViewSchema.Version });
        Assert.Equal(0, Count(path, "view_items"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Schema_bump_rebuilds_on_start()
    {
        var path = Path.Combine(_home.Path, "views.db");
        var runner = new MigrationRunner(new SqliteConnectionFactory(new SqliteOptions(path)), new FakeTimeProvider(), backupBeforeUpgrade: false);
        Assert.True((await runner.Migrate([.. ViewsDatabase.Migrations.Take(3)], Token)).IsSuccess);
        Exec(path, "INSERT INTO projector_state (id, cursor, schema_version, rebuilding) VALUES (1, 12, 3, 0)");
        Exec(path, "INSERT INTO view_services (key, row_json, seq) VALUES ('heartbeat', '{}', 4)");
        Assert.DoesNotContain("view_jobs", Tables(path), StringComparer.Ordinal);

        using var views = new SqliteViews(_home.Path);
        var opened = (await views.Store.Open(ViewSchema.Version, Token)).Value;

        Assert.Equal(3, opened.SchemaVersion);   // the file's own version: the projector sees it differs and resets
        Assert.Contains("view_jobs", Tables(path), StringComparer.Ordinal);
        Assert.Equal(1, Count(path, "view_services"));   // the migration only adds the table
        var reset = (await views.Store.Reset(ViewNames.All, ViewSchema.Version, new FakeTimeProvider().GetUtcNow(), 0, Token)).Value;
        Assert.True(reset is { Cursor: 0, Rebuilding: true, SchemaVersion: ViewSchema.Version });
        Assert.Equal(0, Count(path, "view_services"));
    }

    private static long Count(string path, string table)
    {
        using var connection = new SqliteConnection($"Data Source={path};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {table}";
        return (long)command.ExecuteScalar()!;
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_views_db_at_schema_version_1_gains_the_item_tables_and_is_rebuilt()
    {
        var path = Path.Combine(_home.Path, "views.db");
        var runner = new MigrationRunner(new SqliteConnectionFactory(new SqliteOptions(path)), new FakeTimeProvider(), backupBeforeUpgrade: false);
        Assert.True((await runner.Migrate([ViewsDatabase.Migrations[0]], Token)).IsSuccess);
        Exec(path, "INSERT INTO projector_state (id, cursor, schema_version, rebuilding) VALUES (1, 7, 1, 0)");
        Assert.DoesNotContain("view_items", Tables(path), StringComparer.Ordinal);

        using var views = new SqliteViews(_home.Path);
        var opened = (await views.Store.Open(ViewSchema.Version, Token)).Value;

        Assert.Equal(1, opened.SchemaVersion);   // the file's own version: the projector sees it differs and resets
        Assert.Contains("view_items", Tables(path), StringComparer.Ordinal);
        var reset = (await views.Store.Reset(ViewNames.All, ViewSchema.Version, new FakeTimeProvider().GetUtcNow(), 0, Token)).Value;
        Assert.True(reset is { Cursor: 0, Rebuilding: true, SchemaVersion: ViewSchema.Version });
    }

    private static void Exec(string path, string sql)
    {
        using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        _ = command.ExecuteNonQuery();
    }

    private static List<string> Columns(string path, string table)
    {
        using var connection = new SqliteConnection($"Data Source={path};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT name FROM pragma_table_xinfo('{table}') ORDER BY cid";
        using var reader = command.ExecuteReader();
        var names = new List<string>();
        while (reader.Read())
        {
            names.Add(reader.GetString(0));
        }

        return names;
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
