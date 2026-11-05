using System.Security.Cryptography;
using Microsoft.Data.Sqlite;

namespace Aw.Kernel.Data.Tests;

[Trait("Lane", "integration")]
public sealed class MigrationSafetyTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static MigrationRunner Runner(TempDatabase db) => new(db.Factory(), new FixedClock(DateTimeOffset.UnixEpoch));

    private static Migration Parent => new(
        1,
        "parent",
        "CREATE TABLE parent (id INTEGER PRIMARY KEY, name TEXT);\n" +
        "CREATE TABLE child (id INTEGER PRIMARY KEY, parent_id INTEGER NOT NULL REFERENCES parent (id) ON DELETE CASCADE);\n");

    // The SQLite 12-step table rebuild (https://sqlite.org/lang_altertable.html#otheralter).
    private static Migration RebuildParent => new(
        2,
        "rebuild_parent",
        "CREATE TABLE parent_new (id INTEGER PRIMARY KEY, name TEXT NOT NULL DEFAULT '');\n" +
        "INSERT INTO parent_new (id, name) SELECT id, COALESCE(name, '') FROM parent;\n" +
        "DROP TABLE parent;\n" +
        "ALTER TABLE parent_new RENAME TO parent;\n");

    private static async Task<long> Scalar(TempDatabase db, string sql)
    {
        await using var connection = new SqliteConnection($"Data Source={db.Path};Pooling=False");
        await connection.OpenAsync(Ct);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (long)(await command.ExecuteScalarAsync(Ct))!;
    }

    private static async Task Exec(string path, string sql)
    {
        await using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        await connection.OpenAsync(Ct);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        _ = await command.ExecuteNonQueryAsync(Ct);
    }

    private static string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));

    [Fact]
    public async Task A_parent_table_rebuild_keeps_the_cascading_child_rows()
    {
        using var db = new TempDatabase();
        _ = await Runner(db).Migrate([Parent], Ct);
        await Exec(db.Path, "INSERT INTO parent VALUES (1, 'a'); INSERT INTO child VALUES (10, 1), (11, 1);");

        var rebuilt = await Runner(db).Migrate([Parent, RebuildParent], Ct);

        Assert.True(rebuilt.IsSuccess);
        Assert.Equal(2L, await Scalar(db, "SELECT count(*) FROM child"));
        Assert.Equal(0L, await Scalar(db, "SELECT count(*) FROM pragma_foreign_key_check"));
    }

    [Fact]
    public async Task Foreign_keys_are_back_on_after_migrating()
    {
        using var db = new TempDatabase();
        _ = await Runner(db).Migrate([Parent], Ct);
        await using var work = (await new SqliteUnitOfWorkFactory(db.Factory()).BeginSession(Ct)).Value;

        var orphan = await SqliteQueries.Execute(work, new SqlStatement("INSERT INTO child VALUES (1, 999)"), Ct);

        Assert.Equal(DataErrors.Constraint.Code, orphan.Error.Code);
    }

    [Fact]
    public async Task A_migration_that_leaves_foreign_key_violations_is_rolled_back_as_a_constraint_error()
    {
        using var db = new TempDatabase();
        _ = await Runner(db).Migrate([Parent], Ct);
        var orphaning = new Migration(2, "orphan", "INSERT INTO child VALUES (1, 999);");

        var refused = await Runner(db).Migrate([Parent, orphaning], Ct);

        Assert.Equal(DataErrors.Constraint.Code, refused.Error.Code);
        Assert.Contains("child", refused.Error.Message, StringComparison.Ordinal);
        Assert.Equal(0L, await Scalar(db, "SELECT count(*) FROM child"));
        Assert.Equal(1L, await Scalar(db, "SELECT count(*) FROM schema_version"));
    }

    [Fact]
    public async Task A_database_the_runner_does_not_manage_is_refused_and_left_byte_for_byte_unchanged()
    {
        using var db = new TempDatabase();
        await Exec(db.Path, "CREATE TABLE events (id INTEGER PRIMARY KEY); PRAGMA journal_mode = DELETE;");
        var before = Hash(db.Path);

        var refused = await Runner(db).Migrate([Schema.Items], Ct);

        Assert.Equal(DataErrors.ForeignDatabase.Code, refused.Error.Code);
        Assert.Equal(before, Hash(db.Path));
        Assert.False(File.Exists(db.Path + "-wal"));
    }

    [Fact]
    public async Task Upgrading_a_database_with_applied_migrations_backs_it_up_first()
    {
        using var db = new TempDatabase();
        _ = await Runner(db).Migrate([Schema.Items], Ct);
        await Exec(db.Path, "INSERT INTO items VALUES ('a', 'alpha', 1)");

        _ = await Runner(db).Migrate([Schema.Items, Schema.Tags], Ct);

        var backup = db.Path + ".bak-v1";
        Assert.True(File.Exists(backup));
        await using var copy = new SqliteConnection($"Data Source={backup};Mode=ReadOnly;Pooling=False");
        await copy.OpenAsync(Ct);
        await using var command = copy.CreateCommand();
        command.CommandText = "SELECT (SELECT count(*) FROM items) * 10 + (SELECT max(version) FROM schema_version)";
        Assert.Equal(11L, (long)(await command.ExecuteScalarAsync(Ct))!);
        Assert.Empty(Directory.GetFiles(db.Directory, "*.tmp*"));
    }

    [Fact]
    public async Task No_backup_for_a_new_database_or_when_nothing_is_pending_and_the_first_backup_is_kept()
    {
        using var db = new TempDatabase();
        _ = await Runner(db).Migrate([Schema.Items], Ct);
        Assert.False(File.Exists(db.Path + ".bak-v0"));
        await File.WriteAllTextAsync(db.Path + ".bak-v1", "first", Ct);

        _ = await Runner(db).Migrate([Schema.Items, Schema.Tags], Ct);
        _ = await Runner(db).Migrate([Schema.Items, Schema.Tags], Ct);

        Assert.Equal("first", await File.ReadAllTextAsync(db.Path + ".bak-v1", Ct));
        Assert.False(File.Exists(db.Path + ".bak-v2"));
    }

    [Fact]
    public async Task A_successful_upgrade_keeps_only_the_newest_backup()
    {
        using var db = new TempDatabase();
        _ = await Runner(db).Migrate([Schema.Items], Ct);
        _ = await Runner(db).Migrate([Schema.Items, Schema.Tags], Ct);
        _ = await Runner(db).Migrate([Schema.Items, Schema.Tags, Schema.Notes], Ct);

        Assert.True(File.Exists(db.Path + ".bak-v2"));
        Assert.False(File.Exists(db.Path + ".bak-v1"));
    }

    [Fact]
    public async Task Pruning_leaves_temp_files_and_other_names_alone()
    {
        using var db = new TempDatabase();
        _ = await Runner(db).Migrate([Schema.Items], Ct);
        _ = await Runner(db).Migrate([Schema.Items, Schema.Tags], Ct);
        string[] bystanders =
        [
            db.Path + ".bak-v1.tmp-1-x",
            db.Path + ".bak-vX",
            Path.Combine(db.Directory, "other.db.bak-v1"),
        ];
        foreach (var path in bystanders)
        {
            await File.WriteAllTextAsync(path, "keep", Ct);
        }

        _ = await Runner(db).Migrate([Schema.Items, Schema.Tags, Schema.Notes], Ct);

        Assert.False(File.Exists(db.Path + ".bak-v1"));
        Assert.True(File.Exists(db.Path + ".bak-v2"));
        Assert.All(bystanders, path => Assert.True(File.Exists(path), path));
    }

    [Fact]
    public async Task A_failed_migration_prunes_nothing()
    {
        using var db = new TempDatabase();
        var extra = new Migration(2, "extra", "CREATE TABLE extra (id INTEGER PRIMARY KEY);");
        var orphaning = new Migration(3, "orphan", "INSERT INTO child VALUES (1, 999);");
        _ = await Runner(db).Migrate([Parent], Ct);
        _ = await Runner(db).Migrate([Parent, extra], Ct);

        var refused = await Runner(db).Migrate([Parent, extra, orphaning], Ct);

        Assert.Equal(DataErrors.Constraint.Code, refused.Error.Code);
        Assert.True(File.Exists(db.Path + ".bak-v1"));
        Assert.True(File.Exists(db.Path + ".bak-v2"));
    }

    [Fact]
    public async Task An_upgrade_backup_is_readable_only_by_its_owner()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Unix file modes");
            return;
        }

        using var db = new TempDatabase();
        _ = await Runner(db).Migrate([Schema.Items], Ct);
        _ = await Runner(db).Migrate([Schema.Items, Schema.Tags], Ct);

        var mode = File.GetUnixFileMode(db.Path + ".bak-v1");

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, mode);
    }

    [Fact]
    public async Task An_existing_group_readable_backup_is_made_owner_only_on_the_next_upgrade()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Unix file modes");
            return;
        }

        using var db = new TempDatabase();
        _ = await Runner(db).Migrate([Schema.Items], Ct);
        var old = db.Path + ".bak-v1";
        await File.WriteAllTextAsync(old, "written before backups were owner-only", Ct);
        File.SetUnixFileMode(old, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);

        _ = await Runner(db).Migrate([Schema.Items, Schema.Tags], Ct);

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(old));
    }

    [Fact]
    public async Task Two_runners_at_once_apply_each_migration_once_and_both_report_the_final_version()
    {
        using var db = new TempDatabase();
        Migration[] set = [Schema.Items, Schema.Tags, Schema.Notes];

        var reports = await Task.WhenAll(
            Task.Run(async () => await Runner(db).Migrate(set, Ct), Ct),
            Task.Run(async () => await Runner(db).Migrate(set, Ct), Ct));

        Assert.All(reports, r => Assert.Equal(3, r.Value.ToVersion));
        Assert.Equal([1, 2, 3], reports.SelectMany(r => r.Value.Applied).Order());
        Assert.Equal(3L, await Scalar(db, "SELECT count(*) FROM schema_version"));
    }

    [Theory]
    [InlineData("DELETE FROM schema_version WHERE version = 2")]
    [InlineData("UPDATE schema_version SET version = 0 WHERE version = 1")]
    [InlineData("UPDATE schema_version SET name = 'renamed' WHERE version = 1")]
    public async Task A_damaged_applied_history_is_refused(string damage)
    {
        using var db = new TempDatabase();
        _ = await Runner(db).Migrate([Schema.Items, Schema.Tags, Schema.Notes], Ct);
        await Exec(db.Path, damage);

        var refused = await Runner(db).Migrate([Schema.Items, Schema.Tags, Schema.Notes], Ct);

        Assert.Equal(DataErrors.MigrationDrift.Code, refused.Error.Code);
    }

    [Fact]
    public async Task An_empty_migration_set_is_refused()
    {
        using var db = new TempDatabase();

        var refused = await Runner(db).Migrate([], Ct);

        Assert.Equal(DataErrors.MigrationSequence.Code, refused.Error.Code);
        Assert.False(File.Exists(db.Path));
    }

    [Fact]
    public async Task A_foreign_schema_version_table_is_not_taken_as_ours_and_the_file_is_left_unchanged()
    {
        using var db = new TempDatabase();
        await Exec(db.Path, "CREATE TABLE schema_version (version INTEGER); INSERT INTO schema_version VALUES (1); CREATE TABLE events (id INTEGER PRIMARY KEY); PRAGMA journal_mode = DELETE;");
        var before = Hash(db.Path);

        var refused = await Runner(db).Migrate([Schema.Items], Ct);

        Assert.Equal(DataErrors.ForeignDatabase.Code, refused.Error.Code);
        Assert.Contains("schema_version", refused.Error.Message, StringComparison.Ordinal);
        Assert.Equal(before, Hash(db.Path));
        Assert.False(File.Exists(db.Path + "-wal"));
    }

    [Fact]
    public async Task History_errors_are_found_before_anything_is_written()
    {
        using var db = new TempDatabase();
        _ = await Runner(db).Migrate([Schema.Items, Schema.Tags], Ct);
        await Exec(db.Path, "PRAGMA journal_mode = DELETE;");
        var before = Hash(db.Path);

        var newer = await Runner(db).Migrate([Schema.Items], Ct);
        var drifted = await Runner(db).Migrate([Schema.Items with { Sql = Schema.Items.Sql + " -- edited" }, Schema.Tags], Ct);

        Assert.Equal(DataErrors.MigrationUnknown.Code, newer.Error.Code);
        Assert.Equal(DataErrors.MigrationDrift.Code, drifted.Error.Code);
        Assert.Equal(before, Hash(db.Path));
        Assert.False(File.Exists(db.Path + "-wal"));
    }
}
