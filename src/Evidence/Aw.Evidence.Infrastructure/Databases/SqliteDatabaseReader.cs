using Aw.Evidence.Core;
using Aw.Evidence.Core.Data;
using Aw.Kernel;
using Aw.Kernel.Data;
using Microsoft.Data.Sqlite;

namespace Aw.Evidence.Infrastructure.Databases;

/// <summary>
/// Reads a SQLite file through a copy of it. The source is only ever read as a file (File.Copy of the database, its -wal and its -journal into the work
/// directory, verified by stat before and after, retried if it changed, then opened once read-write on the copy so a hot journal rolls back there); no SQLite connection opens it. The session then queries the copy opened read-only with query_only (Aw.Kernel.Data's OpenReadOnly).
/// </summary>
internal sealed class SqliteDatabaseReader : IDatabaseReader
{
    private readonly Action<int>? _afterCopy;

    public SqliteDatabaseReader()
    {
    }

    /// <summary>A reader that calls <paramref name="afterCopy"/> with the attempt number after each copy of the source (tests change the source there).</summary>
    internal SqliteDatabaseReader(Action<int> afterCopy)
    {
        _afterCopy = afterCopy;
    }

    internal const string CopyName = "evidence-copy.sqlite";

    private const int NoAttachedDatabases = 7;   // SQLITE_LIMIT_ATTACHED
    private const int CopyAttempts = 3;
    private const int MaxLinks = 40;
    private const int NotADatabaseCode = 26;

    // The main file and the sidecars the copy carries; -shm is never copied (SQLite rebuilds it).
    private static readonly string[] _sidecars = ["", "-wal", "-journal", "-shm"];

    /// <inheritdoc/>
    public async ValueTask<Result<IReadSession>> OpenAsync(DbTarget target, CancellationToken cancellationToken)
    {
        if (target is not SqliteTarget sqlite)
        {
            return EvidenceErrors.DatabaseRefused.With("this reader opens SQLite files only");
        }

        if (!File.Exists(sqlite.SourcePath))
        {
            return EvidenceErrors.DatabaseUnavailable.With($"{sqlite.SourcePath} is not a file");
        }

        var copy = Path.Combine(sqlite.WorkDirectory, CopyName);
        var copied = await CopyAsync(sqlite, copy, cancellationToken).ConfigureAwait(false);
        if (copied.IsError)
        {
            return copied.Error;
        }

        var stable = await StabilizeAsync(copy, sqlite.SourcePath, cancellationToken).ConfigureAwait(false);
        if (stable.IsError)
        {
            return stable.Error;
        }

        var seeded = await SeedAsync(sqlite.SeedSql, copy, cancellationToken).ConfigureAwait(false);
        if (seeded.IsError)
        {
            return seeded.Error;
        }

        var opened = await new SqliteConnectionFactory(new SqliteOptions(copy)).OpenReadOnly(cancellationToken).ConfigureAwait(false);
        return opened.IsError ? EvidenceErrors.DatabaseUnavailable.With($"can't open the copy {copy}: {opened.Error.Message}") : new SqliteReadSession(opened.Value);
    }

    // The seed runs on its own read-write connection to the copy, closed before the read-only one opens; the source is never touched. The statement guard
    // refuses ATTACH, DETACH, VACUUM and load_extension, and the connection allows no attached database at all (SQLITE_LIMIT_ATTACHED = 0), so no other file is reachable.
    private static async ValueTask<Result<Unit>> SeedAsync(string? seedSql, string copy, CancellationToken cancellationToken)
    {
        if (seedSql is null)
        {
            return Unit.Value;
        }

        var allowed = SeedStatements.Check(seedSql);
        if (allowed.IsError)
        {
            return allowed.Error;
        }

        var opened = await OpenSeedConnectionAsync(copy, cancellationToken).ConfigureAwait(false);
        if (opened.IsError)
        {
            return EvidenceErrors.DatabaseUnavailable.With($"can't open the copy {copy} to seed it: {opened.Error.Message}");
        }

        await using (opened.Value.ConfigureAwait(false))
        {
            try
            {
                var command = opened.Value.CreateCommand();
                await using (command.ConfigureAwait(false))
                {
                    command.CommandText = seedSql;
                    _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            catch (SqliteException exception)
            {
                return EvidenceErrors.DbScriptInvalid.With($"the seed failed on the copy: {exception.Message}");
            }
        }

        return Unit.Value;
    }

    // Read-write on the copy with no attached database allowed (SQLITE_LIMIT_ATTACHED = 0).
    internal static async ValueTask<Result<SqliteConnection>> OpenSeedConnectionAsync(string copy, CancellationToken cancellationToken)
    {
        var opened = await new SqliteConnectionFactory(new SqliteOptions(copy)).Open(cancellationToken).ConfigureAwait(false);
        if (opened.IsSuccess)
        {
            _ = SQLitePCL.raw.sqlite3_limit(opened.Value.Handle, NoAttachedDatabases, 0);
        }

        return opened;
    }

    // A plain file copy of the main file, its -wal and its -journal (never -shm), so no SQLite connection ever opens the source: even a read-only
    // connection creates -shm and -wal beside a WAL-mode database. The copy takes none of SQLite's locks, so it is verified instead: the three source
    // files are stat'ed (length and modification time) before and after, and a change means the copy may pair files of different moments, so it is
    // made again (CopyAttempts times). Stabilize then rolls a hot journal back on the copy and checks it.
    private ValueTask<Result<Unit>> CopyAsync(SqliteTarget target, string copy, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var work = RealPath(Path.GetFullPath(target.WorkDirectory));
        var source = RealPath(Path.GetFullPath(target.SourcePath));
        if (IsInside(work, source))
        {
            return ValueTask.FromResult<Result<Unit>>(EvidenceErrors.DatabaseRefused.With($"{target.SourcePath} is inside the run's own database directory"));
        }

        try
        {
            _ = Directory.CreateDirectory(work);
            copy = Path.Combine(work, Path.GetFileName(copy));
            for (var attempt = 1; attempt <= CopyAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var before = Stat(source);
                var complete = CopyOnce(source, copy);
                _afterCopy?.Invoke(attempt);
                if (complete && before == Stat(source))
                {
                    return ValueTask.FromResult<Result<Unit>>(Unit.Value);
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return ValueTask.FromResult<Result<Unit>>(EvidenceErrors.DatabaseUnavailable.With($"can't copy {target.SourcePath}: {exception.Message}"));
        }

        return ValueTask.FromResult<Result<Unit>>(EvidenceErrors.DatabaseUnavailable.With($"{target.SourcePath} changed while it was copied ({CopyAttempts.ToString(System.Globalization.CultureInfo.InvariantCulture)} tries); stop the app's writes to it, or retry"));
    }

    // False when a sidecar vanished between the existence check and the copy (a commit finished): the caller counts that as a change.
    private static bool CopyOnce(string source, string copy)
    {
        foreach (var suffix in _sidecars)
        {
            File.Delete(copy + suffix);
        }

        File.Copy(source, copy, overwrite: true);
        foreach (var suffix in _sidecars.Where(static s => s.Length > 0 && s != "-shm"))
        {
            try
            {
                if (File.Exists(source + suffix))
                {
                    File.Copy(source + suffix, copy + suffix);
                }
            }
            catch (FileNotFoundException)
            {
                return false;
            }
        }

        return true;
    }

    // Length and last write time of the main file, -wal and -journal, as one comparable text.
    private static string Stat(string source) => string.Join(
        '|',
        _sidecars.Where(static s => s != "-shm").Select(suffix => new FileInfo(source + suffix) is { Exists: true } info ? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{info.Length}:{info.LastWriteTimeUtc.Ticks}") : "-"));

    // A file that SQLite says is not a database (SQLITE_NOTADB) is the script's input being wrong, like a seed that fails: AW-EVIDENCE-018, exit 1. It is not a database that may recover.
    private static Error NotADatabase(string sourcePath) => EvidenceErrors.DbScriptInvalid.With($"{sourcePath} is not a SQLite database");

    // The copy opened read-write once: SQLite rolls a hot journal back (or recovers a -wal) on the copy, and quick_check proves the result is a database.
    private static async ValueTask<Result<Unit>> StabilizeAsync(string copy, string sourcePath, CancellationToken cancellationToken)
    {
        var unstable = EvidenceErrors.DatabaseUnavailable;
        var opened = await OpenSeedConnectionAsync(copy, cancellationToken).ConfigureAwait(false);
        if (opened.IsError)
        {
            return opened.Error.Message.Contains("not a database", StringComparison.OrdinalIgnoreCase)
                ? NotADatabase(sourcePath)
                : unstable.With($"the copy of {sourcePath} can't be opened ({opened.Error.Message}); stop the app's writes to it, or retry");
        }

        await using (opened.Value.ConfigureAwait(false))
        {
            try
            {
                var command = opened.Value.CreateCommand();
                await using (command.ConfigureAwait(false))
                {
                    command.CommandText = "PRAGMA quick_check";
                    var verdict = Convert.ToString(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), System.Globalization.CultureInfo.InvariantCulture);
                    return verdict == "ok" ? Unit.Value : unstable.With($"the copy of {sourcePath} is not a consistent database (quick_check: {verdict}); stop the app's writes to it, or retry");
                }
            }
            catch (SqliteException exception) when (exception.SqliteErrorCode == NotADatabaseCode)
            {
                return NotADatabase(sourcePath);
            }
            catch (SqliteException exception)
            {
                return unstable.With($"the copy of {sourcePath} is not a consistent database ({exception.Message}); stop the app's writes to it, or retry");
            }
        }
    }

    // The path with every symbolic link in it resolved (a missing part stays as written), so two spellings of one place compare equal.
    private static string RealPath(string path)
    {
        var resolved = Path.GetPathRoot(path) ?? string.Empty;
        var links = 0;
        foreach (var part in path[resolved.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            resolved = Path.Combine(resolved, part);
            while (links++ < MaxLinks && new FileInfo(resolved).LinkTarget is { } target)
            {
                resolved = Path.GetFullPath(target, Path.GetDirectoryName(resolved) ?? resolved);
            }
        }

        return resolved;
    }

    private static bool IsInside(string directory, string path) =>
        path.StartsWith(directory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal);
}
