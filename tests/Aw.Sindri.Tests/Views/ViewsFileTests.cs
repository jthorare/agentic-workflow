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
        Assert.Equal(["projector_state", "view_code_index", "view_decisions", "view_item_aliases", "view_item_links", "view_item_timeline", "view_items", "view_jobs", "view_launches", "view_model_history", "view_model_runs", "view_needs_you", "view_profile", "view_project_attached", "view_project_hidden", "view_requests", "view_services", "view_todo", "view_workflow_runs"], Tables(path));
        Assert.Equal(["key", "row_json", "seq", "owner", "state"], Columns(path, "view_items"));
        Assert.Equal(["key", "row_json", "seq", "item", "state", "repo"], Columns(path, "view_requests"));
        Assert.Equal(["key", "row_json", "seq", "item", "state", "repo"], Columns(path, "view_decisions"));
        Assert.Equal(["key", "row_json", "seq", "item", "repo", "kind"], Columns(path, "view_needs_you"));
        Assert.Equal(["key", "row_json", "seq", "state", "kind", "repo"], Columns(path, "view_jobs"));
        Assert.Equal(["key", "row_json", "seq", "item", "state", "repo", "run_id", "node_id", "reason", "instance"], Columns(path, "view_launches"));
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_view_name_has_a_table_and_the_schema_is_at_version_37()
    {
        Assert.Equal(["code-index", "decisions", "item-aliases", "item-links", "item-timeline", "items", "jobs", "launches", "model-history", "model-runs", "needs_you", "profile", "project-attached", "project-hidden", "requests", "services", "todo", "workflow-runs"], ViewNames.All.Order(StringComparer.Ordinal).ToList());
        Assert.Equal(37, ViewSchema.Version);   // the workflow run row has a mode, live or test (#3197); the needs-you row has a decision member (#2789); the items view has a state column (#2788); the launches view has reason and instance columns (#2786); the question rows carry the loop item of an ask_owner node in a for_each body (#2543); the needs-you row carries the question kind (PR 9 ask_owner); the launch row carries the graph run and node (PR7 run_agent); the item-links view joined, recipe-stats left and the engine part left the items row (v2 PR 4b); the recipe engine is gone (#2258): the row-shape hash covers the types the rows hold; the graph runs view (issue 2258 PR 6); the needs-you row carries the connection kind (connections PR 10b); the todo view (todo spec §4.2); the launcher ids moved to Core.Launch, which changes the row shape hash (v2 PR3a); the jobs row keeps its last 200 model calls and counts fallbacks (#1567 R-4); the project-attached row carries notes, not note (owner q158); the jobs row carries its model-call totals and the signal it waits on (#1567 R-3a); the project-attached and project-hidden views (projects page Ph3); a park row carries its sandbox refusal (SB-6); the launch row carries the sandbox summary from launch.started (SB-6); the model-history fold keys a mirrored row by the workflow id (#1481); the launch row carries the model (#1008); a parked suspension carries its sandbox marker and the launch and self-check park kinds (U9, #1113); the model-history and model-runs views (model-pick M-4); the launch row carries the sandbox failure reason, source and limit ending (SB-0); the services row carries pausedBy/pausedAt and lastRun.detail (B-15); the launch row folds launch.takeover-opened (#1004); the engine item part carries the unavailable park (#1001); NeedsYouRow.Open became required (#959); the code-index view joined after recipe-stats (and launch rows gained a required member at 7): an older views.db must rebuild, not fail to read
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
