using System.Security.Cryptography;
using Aw.Evidence.Core.Data;
using Aw.Evidence.Infrastructure.Databases;
using Aw.Evidence.Testing;
using Aw.Kernel.Data;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Infrastructure;

/// <summary>The SQLite reader, called directly (no pre-run check in front of it), against real files in a temp directory.</summary>
[Lane(TestLane.Integration)]
public sealed class SqliteDatabaseReaderContractTests : ReadOnlyDatabaseContract, IDisposable
{
    private readonly TempDirectory _root = new();

    internal static async Task MakeNotesAsync(string path)
    {
        var opened = await new SqliteConnectionFactory(new SqliteOptions(path)).Open(TestContext.Current.CancellationToken);
        await using var connection = opened.Value;
        var command = connection.CreateCommand();
        await using var scope = command.ConfigureAwait(false);
        command.CommandText = "CREATE TABLE notes(id INTEGER PRIMARY KEY, text TEXT); INSERT INTO notes(text) VALUES ('hello'), ('world'), ('again')";
        _ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    protected override async Task<DatabaseUnderTest> ArrangeAsync()
    {
        var source = _root.Combine("repo/data/notes.db");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(source)!);
        await MakeNotesAsync(source);
        var work = _root.Combine("run/db");
        return new DatabaseUnderTest(new SqliteDatabaseReader(), new SqliteTarget(source, work), () => Task.FromResult(Snapshot(_root.Combine("repo"), _root.Combine("run"))));
    }

    protected override IReadOnlyList<(string Name, string Sql)> Bypasses() =>
    [
        ("a writing CTE", "WITH x AS (SELECT 'w' AS t) INSERT INTO notes(text) SELECT t FROM x"),
        ("a PRAGMA assignment", "PRAGMA user_version = 9"),
        ("PRAGMA called as a function", "PRAGMA user_version(9)"),
        ("ATTACH of a new file", $"ATTACH '{_root.Combine("run/new.db")}' AS n"),
        ("VACUUM INTO a new file", $"VACUUM INTO '{_root.Combine("run/vacuumed.db")}'"),
        ("a delete", "DELETE FROM notes"),
        ("DDL", "CREATE TABLE t(a)"),
    ];

    [Fact]
    public async Task The_source_file_is_never_changed_and_the_copy_lives_in_the_work_directory()
    {
        var db = await ArrangeAsync();
        var source = ((SqliteTarget)db.Target).SourcePath;
        var before = Sha(source);

        await using (var session = (await db.Reader.OpenAsync(db.Target, TestContext.Current.CancellationToken)).Value)
        {
            _ = await session.QueryAsync(ReadAll, 10, TestContext.Current.CancellationToken);
            _ = await session.QueryAsync("DELETE FROM notes", 10, TestContext.Current.CancellationToken);
        }

        Assert.Equal(before, Sha(source));
        Assert.True(File.Exists(_root.Combine("run/db/evidence-copy.sqlite")));
    }

    [Fact]
    public async Task A_seed_that_attaches_the_source_or_vacuums_into_a_file_outside_the_run_is_refused_and_touches_nothing()
    {
        var db = await ArrangeAsync();
        var source = ((SqliteTarget)db.Target).SourcePath;
        var outside = _root.Combine("repo/seed-vacuum.db");
        var seed = $"ATTACH '{source}' AS src;\nINSERT INTO src.notes(text) VALUES ('x');\nDETACH src;\nVACUUM INTO '{outside}';\n";
        var before = Sha(source);

        var opened = await new SqliteDatabaseReader().OpenAsync(((SqliteTarget)db.Target) with { SeedSql = seed }, TestContext.Current.CancellationToken);

        Assert.True(opened.IsError);
        Assert.Equal(Core.EvidenceErrors.DatabaseRefused.Code, opened.Error.Code);
        Assert.Equal(before, Sha(source));
        Assert.False(File.Exists(outside));
    }

    [Theory]
    [InlineData("ATTACH ':memory:' AS m")]
    [InlineData("DETACH m")]
    [InlineData("VACUUM")]
    [InlineData("SELECT load_extension('x')")]
    public async Task A_seed_statement_that_reaches_another_file_is_refused(string seed)
    {
        var db = await ArrangeAsync();

        var opened = await new SqliteDatabaseReader().OpenAsync(((SqliteTarget)db.Target) with { SeedSql = seed }, TestContext.Current.CancellationToken);

        Assert.True(opened.IsError);
        Assert.Equal(Core.EvidenceErrors.DatabaseRefused.Code, opened.Error.Code);
    }

    [Fact]
    public async Task A_missing_source_is_unavailable_and_creates_no_file_there()
    {
        var missing = _root.Combine("repo/none.db");
        _ = Directory.CreateDirectory(_root.Combine("repo"));

        var opened = await new SqliteDatabaseReader().OpenAsync(new SqliteTarget(missing, _root.Combine("run/db")), TestContext.Current.CancellationToken);

        Assert.Equal(Core.EvidenceErrors.DatabaseUnavailable.Code, opened.Error.Code);
        Assert.False(File.Exists(missing));
    }

    [Fact]
    public async Task A_WAL_mode_source_leaves_its_directory_exactly_as_it_was()
    {
        var source = _root.Combine("repo/data/wal.db");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(source)!);
        _ = await MakeWalAsync(source, pageSize: 4096, keepOpen: false);
        var before = Listing(Path.GetDirectoryName(source)!);

        await using (var session = (await new SqliteDatabaseReader().OpenAsync(new SqliteTarget(source, _root.Combine("run/db")), TestContext.Current.CancellationToken)).Value)
        {
            var rows = await session.QueryAsync(ReadAll, 10, TestContext.Current.CancellationToken);
            Assert.Equal(3, rows.Value.Count);
        }

        Assert.Equal(before, Listing(Path.GetDirectoryName(source)!));
    }

    [Fact]
    public async Task A_WAL_source_with_committed_rows_still_in_its_wal_file_reads_them_and_stays_untouched()
    {
        var source = _root.Combine("repo/data/live.db");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(source)!);
        await using var holder = await MakeWalAsync(source, pageSize: 4096, keepOpen: true);
        var before = Listing(Path.GetDirectoryName(source)!);

        await using var session = (await new SqliteDatabaseReader().OpenAsync(new SqliteTarget(source, _root.Combine("run/db")), TestContext.Current.CancellationToken)).Value;
        var rows = await session.QueryAsync(ReadAll, 10, TestContext.Current.CancellationToken);

        Assert.Equal(3, rows.Value.Count);
        Assert.Equal(before, Listing(Path.GetDirectoryName(source)!));
    }

    [Theory]
    [InlineData(1024)]
    [InlineData(8192)]
    public async Task A_source_whose_page_size_is_not_4096_is_copied_and_read(int pageSize)
    {
        var source = _root.Combine("repo/data/pages.db");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(source)!);
        _ = await MakeWalAsync(source, pageSize, keepOpen: false);

        var opened = await new SqliteDatabaseReader().OpenAsync(new SqliteTarget(source, _root.Combine("run/db")), TestContext.Current.CancellationToken);

        Assert.True(opened.IsSuccess, opened.IsError ? opened.Error.Message : "");
        await using var session = opened.Value;
        Assert.Equal(3, (await session.QueryAsync(ReadAll, 10, TestContext.Current.CancellationToken)).Value.Count);
    }

    [Fact]
    public async Task A_source_with_a_hot_journal_reads_only_what_was_committed()
    {
        var source = _root.Combine("repo/data/hot.db");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(source)!);
        await MakeNotesAsync(source);
        await using (var writer = await OpenWriterAsync(source))
        {
            await ExecuteAsync(writer, "PRAGMA journal_mode = DELETE; INSERT INTO notes(text) WITH RECURSIVE c(x) AS (SELECT 1 UNION ALL SELECT x + 1 FROM c WHERE x < 500) SELECT 'committed-' || x || hex(randomblob(150)) FROM c");
            // A transaction that spills dirty pages into the file and is still open: what is on disk is a crashed commit (changed pages plus a hot journal).
            await ExecuteAsync(writer, "PRAGMA cache_size = 2; PRAGMA cache_spill = 1; BEGIN; UPDATE notes SET text = 'uncommitted' || id || hex(randomblob(150))");
            File.Copy(source, source + ".crash");
            File.Copy(source + "-journal", source + ".crash-journal");
            await ExecuteAsync(writer, "ROLLBACK");
        }

        File.Move(source + ".crash", source, overwrite: true);
        File.Move(source + ".crash-journal", source + "-journal", overwrite: true);
        var before = Listing(Path.GetDirectoryName(source)!);

        await using var session = (await new SqliteDatabaseReader().OpenAsync(new SqliteTarget(source, _root.Combine("run/db")), TestContext.Current.CancellationToken)).Value;
        var rows = await session.QueryAsync("SELECT count(*) AS n, sum(text LIKE 'uncommitted%') AS u FROM notes", 5, TestContext.Current.CancellationToken);

        Assert.Equal("""[{"n":503,"u":0}]""", rows.Value.ToJson());
        Assert.Equal(before, Listing(Path.GetDirectoryName(source)!));
    }

    [Fact]
    public async Task A_source_that_changes_while_it_is_copied_is_copied_again()
    {
        var db = await ArrangeAsync();
        var source = ((SqliteTarget)db.Target).SourcePath;
        var attempts = new List<int>();
        var reader = new SqliteDatabaseReader(attempt =>
        {
            attempts.Add(attempt);
            if (attempt == 1)
            {
                File.SetLastWriteTimeUtc(source, DateTime.UtcNow.AddMinutes(5));
            }
        });

        var opened = await reader.OpenAsync(db.Target, TestContext.Current.CancellationToken);

        Assert.True(opened.IsSuccess, opened.IsError ? opened.Error.Message : "");
        Assert.Equal([1, 2], attempts);
    }

    [Fact]
    public async Task A_source_that_never_settles_is_unavailable_after_a_few_tries()
    {
        var db = await ArrangeAsync();
        var source = ((SqliteTarget)db.Target).SourcePath;
        var attempts = 0;
        var reader = new SqliteDatabaseReader(attempt =>
        {
            attempts = attempt;
            File.SetLastWriteTimeUtc(source, DateTime.UtcNow.AddMinutes(attempt));
        });

        var opened = await reader.OpenAsync(db.Target, TestContext.Current.CancellationToken);

        Assert.Equal(Core.EvidenceErrors.DatabaseUnavailable.Code, opened.Error.Code);
        Assert.Contains("changed while", opened.Error.Message, StringComparison.Ordinal);
        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task A_source_that_is_not_a_database_is_an_input_error_not_unavailable()
    {
        var source = _root.Combine("repo/data/junk.db");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(source)!);
        await File.WriteAllBytesAsync(source, new byte[8192], TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(source, new string('x', 9000), TestContext.Current.CancellationToken);

        var opened = await new SqliteDatabaseReader().OpenAsync(new SqliteTarget(source, _root.Combine("run/db")), TestContext.Current.CancellationToken);

        Assert.Equal(Core.EvidenceErrors.DbScriptInvalid.Code, opened.Error.Code);
        Assert.DoesNotContain("stop the app", opened.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_source_inside_a_symlinked_run_directory_is_refused_and_not_deleted()
    {
        var real = _root.Combine("run-real");
        _ = Directory.CreateDirectory(Path.Combine(real, "db"));
        var link = _root.Combine("run-link");
        _ = Directory.CreateSymbolicLink(link, real);
        var source = Path.Combine(real, "db", SqliteDatabaseReader.CopyName);
        await MakeNotesAsync(source);

        var opened = await new SqliteDatabaseReader().OpenAsync(new SqliteTarget(source, Path.Combine(link, "db")), TestContext.Current.CancellationToken);

        Assert.Equal(Core.EvidenceErrors.DatabaseRefused.Code, opened.Error.Code);
        Assert.True(File.Exists(source));
    }

    [Fact]
    public async Task A_source_that_is_a_symlink_into_the_work_directory_is_refused_and_not_deleted()
    {
        var work = _root.Combine("run/db");
        _ = Directory.CreateDirectory(work);
        var inside = Path.Combine(work, SqliteDatabaseReader.CopyName);
        await MakeNotesAsync(inside);
        _ = Directory.CreateDirectory(_root.Combine("repo"));
        var source = _root.Combine("repo/linked.db");
        _ = File.CreateSymbolicLink(source, inside);

        var opened = await new SqliteDatabaseReader().OpenAsync(new SqliteTarget(source, work), TestContext.Current.CancellationToken);

        Assert.Equal(Core.EvidenceErrors.DatabaseRefused.Code, opened.Error.Code);
        Assert.True(File.Exists(inside));
    }

    [Fact]
    public async Task A_source_reached_through_a_symlinked_directory_into_the_work_directory_is_refused()
    {
        var work = _root.Combine("run/db");
        _ = Directory.CreateDirectory(work);
        var inside = Path.Combine(work, SqliteDatabaseReader.CopyName);
        await MakeNotesAsync(inside);
        _ = Directory.CreateDirectory(_root.Combine("repo"));
        _ = Directory.CreateSymbolicLink(_root.Combine("repo/linkdir"), work);

        var opened = await new SqliteDatabaseReader().OpenAsync(new SqliteTarget(_root.Combine("repo/linkdir/" + SqliteDatabaseReader.CopyName), work), TestContext.Current.CancellationToken);

        Assert.Equal(Core.EvidenceErrors.DatabaseRefused.Code, opened.Error.Code);
        Assert.True(File.Exists(inside));
    }

    [Fact]
    public async Task A_source_inside_the_work_directory_is_refused_and_not_deleted()
    {
        var work = _root.Combine("run/db");
        _ = Directory.CreateDirectory(work);
        var source = Path.Combine(work, SqliteDatabaseReader.CopyName);
        await MakeNotesAsync(source);

        var opened = await new SqliteDatabaseReader().OpenAsync(new SqliteTarget(source, work), TestContext.Current.CancellationToken);

        Assert.Equal(Core.EvidenceErrors.DatabaseRefused.Code, opened.Error.Code);
        Assert.True(File.Exists(source));
    }

    [Fact]
    public async Task A_seed_that_fails_on_the_copy_is_018_and_leaves_the_source_alone()
    {
        var db = await ArrangeAsync();
        var source = ((SqliteTarget)db.Target).SourcePath;
        var before = Sha(source);

        var opened = await new SqliteDatabaseReader().OpenAsync(((SqliteTarget)db.Target) with { SeedSql = "INSERT INTO no_such_table VALUES (1)" }, TestContext.Current.CancellationToken);

        Assert.Equal(Core.EvidenceErrors.DbScriptInvalid.Code, opened.Error.Code);
        Assert.Equal(before, Sha(source));
    }

    [Fact]
    public async Task The_seed_connection_allows_no_attached_database()
    {
        var db = await ArrangeAsync();
        var copy = _root.Combine("run/db/seed-copy.sqlite");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
        File.Copy(((SqliteTarget)db.Target).SourcePath, copy);

        var opened = await SqliteDatabaseReader.OpenSeedConnectionAsync(copy, TestContext.Current.CancellationToken);

        await using var connection = opened.Value;
        Assert.Equal(0, SQLitePCL.raw.sqlite3_limit(connection.Handle, 7, -1));
    }

    private static async Task<Microsoft.Data.Sqlite.SqliteConnection> OpenWriterAsync(string path)
    {
        var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={path};Pooling=False");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        return connection;
    }

    private static async Task ExecuteAsync(Microsoft.Data.Sqlite.SqliteConnection connection, string sql)
    {
        var command = connection.CreateCommand();
        await using (command.ConfigureAwait(false))
        {
            command.CommandText = sql;
            _ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }
    }

    private static async Task<Microsoft.Data.Sqlite.SqliteConnection> MakeWalAsync(string path, int pageSize, bool keepOpen)
    {
        var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={path};Pooling=False");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var command = connection.CreateCommand();
        await using (command.ConfigureAwait(false))
        {
            command.CommandText = $"PRAGMA page_size = {pageSize.ToString(System.Globalization.CultureInfo.InvariantCulture)}; PRAGMA journal_mode = WAL; PRAGMA wal_autocheckpoint = 0; CREATE TABLE notes(id INTEGER PRIMARY KEY, text TEXT); INSERT INTO notes(text) VALUES ('hello'), ('world'), ('again')";
            _ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        if (!keepOpen)
        {
            await connection.DisposeAsync();
        }

        return connection;
    }

    // Names, sizes and modification times of everything in the directory, sidecar files included.
    private static string Listing(string directory) => string.Join(
        '\n',
        new DirectoryInfo(directory).EnumerateFiles().OrderBy(static f => f.Name, StringComparer.Ordinal).Select(static f => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{f.Name} {f.Length} {f.LastWriteTimeUtc.Ticks}")));

    private static string Sha(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    // The main files and the names of everything else beside them; SQLite's -wal and -shm files come and go with a connection.
    private static string Snapshot(params string[] directories) => string.Join(
        '\n',
        directories.Where(Directory.Exists).SelectMany(d => Directory.EnumerateFiles(d, "*", SearchOption.AllDirectories))
            .Where(static f => !f.EndsWith("-wal", StringComparison.Ordinal) && !f.EndsWith("-shm", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal).Select(static f => f + " " + Sha(f)));

    public void Dispose() => _root.Dispose();
}
