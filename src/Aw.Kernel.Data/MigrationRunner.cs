using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Aw.Kernel.Data;

/// <summary>
/// Brings a database's schema up to date from numbered migrations (spec §4b.2).
/// <list type="bullet">
/// <item>It checks the migration set, then, on a read-only connection, refuses a database it didn't create (user
/// tables but no schema_version, or a schema_version with other columns: usually a path pointing at the wrong file,
/// AW-DATA-009) and an applied history that doesn't match the build (AW-DATA-006, AW-DATA-007). A refused file is left
/// byte-for-byte unchanged: only then does the read-write open switch it to WAL.</item>
/// <item>Before upgrading a database that already has migrations, it writes a consistent copy with VACUUM INTO to
/// 'file.bak-vN' (the first backup of a version is kept), owner-only on Unix. Once the upgrade succeeds, older backups
/// of the file are removed: the newest one and the one this run took or kept stay.</item>
/// <item>Each migration runs in its own transaction with its schema_version row, with foreign keys off (the SQLite
/// table-rebuild recipe needs that; the pragma is a no-op inside a transaction, so it's switched before BEGIN) and a
/// foreign_key_check before commit. Violations roll back as AW-DATA-004.</item>
/// </list>
/// A migration whose SQL fails is a bug: it rolls back and the exception propagates. The C# build starts from fresh
/// state (no TS-written database is read or migrated), so the runner keeps its version only in schema_version.
/// </summary>
/// <param name="connections">Opens the database.</param>
/// <param name="clock">Stamps applied_at (UTC round-trip text, as SqliteBind writes timestamps).</param>
/// <param name="backupBeforeUpgrade">Write 'file.bak-vN' before upgrading from version N &gt; 0. Default on.</param>
public sealed class MigrationRunner(ISqliteConnectionFactory connections, TimeProvider clock, bool backupBeforeUpgrade = true)
{
    internal const string CreateVersionTable =
        "CREATE TABLE IF NOT EXISTS schema_version (version INTEGER PRIMARY KEY, name TEXT NOT NULL, checksum TEXT NOT NULL, applied_at TEXT NOT NULL)";

    internal const string SelectApplied = "SELECT version, name, checksum FROM schema_version ORDER BY version";

    internal const string SelectCurrent = "SELECT COALESCE(MAX(version), 0) FROM schema_version";

    internal const string InsertApplied = "INSERT INTO schema_version (version, name, checksum, applied_at) VALUES ($v, $n, $c, $t)";

    internal const string VersionColumns = "version,name,checksum,applied_at";

    internal const string SelectOwnership =
        "SELECT (SELECT group_concat(name, ',') FROM (SELECT name FROM pragma_table_info('schema_version') ORDER BY cid)), " +
        "EXISTS (SELECT 1 FROM sqlite_master WHERE type = 'table' AND name <> 'schema_version' AND name NOT LIKE 'sqlite\\_%' ESCAPE '\\')";

    internal const string SelectViolations = "SELECT \"table\", parent FROM pragma_foreign_key_check LIMIT 5";

    /// <summary>Applies every pending migration, or those up to <paramref name="targetVersion"/>.</summary>
    /// <param name="migrations">The full migration set, versions 1..n in order.</param>
    /// <param name="cancellationToken">Cancels the run between migrations.</param>
    /// <param name="targetVersion">Stop after this version; null applies all. Lets a test check each step on the previous schema.</param>
    /// <returns>What was applied, or AW-DATA-002/003/004/005/006/007/008/009.</returns>
    public async ValueTask<Result<MigrationReport>> Migrate(IReadOnlyList<Migration> migrations, CancellationToken cancellationToken, int? targetVersion = null)
    {
        ArgumentNullException.ThrowIfNull(migrations);
        var sequence = MigrationChecks.CheckSequence(migrations);
        if (sequence.IsError)
        {
            return sequence.Error;
        }

        var ownership = await Inspect(migrations, cancellationToken).ConfigureAwait(false);
        if (ownership.IsError)
        {
            return ownership.Error;
        }

        var opened = await connections.Open(cancellationToken).ConfigureAwait(false);
        if (opened.IsError)
        {
            return opened.Error;
        }

        var connection = opened.Value;
        await using var connectionScope = connection.ConfigureAwait(false);
        return await MigrateOpen(connection, migrations, targetVersion, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<MigrationReport>> MigrateOpen(SqliteConnection connection, IReadOnlyList<Migration> migrations, int? targetVersion, CancellationToken cancellationToken)
    {
        try
        {
            await Run(connection, CreateVersionTable, cancellationToken).ConfigureAwait(false);
            var applied = await MigrationChecks.CheckApplied(connection, migrations, cancellationToken).ConfigureAwait(false);
            if (applied.IsError)
            {
                return applied.Error;
            }

            var target = Math.Min(targetVersion ?? migrations.Count, migrations.Count);
            var backedUp = backupBeforeUpgrade && applied.Value > 0 && applied.Value < target;
            if (backedUp)
            {
                await Backup(connection, applied.Value, cancellationToken).ConfigureAwait(false);
            }

            var report = await ApplyPending(connection, migrations, applied.Value, target, cancellationToken).ConfigureAwait(false);
            if (backedUp && !report.IsError)
            {
                MigrationBackups.Prune(connection.DataSource, applied.Value);
            }

            return report;
        }
        catch (SqliteException exception) when (SqliteErrors.TryMap(exception, "Migrating", out var error))
        {
            return error;
        }
    }

    // Everything that can refuse the file runs on a read-only connection first: the read-write open switches the
    // journal to WAL, which rewrites the header, so a refused file must never get that far.
    private async Task<Result<Unit>> Inspect(IReadOnlyList<Migration> migrations, CancellationToken cancellationToken)
    {
        var opened = await connections.OpenReadOnly(cancellationToken).ConfigureAwait(false);
        if (opened.IsError)
        {
            // A missing or unreadable file: the read-write open that follows creates it or reports the real error.
            // A damaged file is reported now.
            return opened.Error.Code == DataErrors.Damaged.Code ? opened.Error : Results.Ok();
        }

        var connection = opened.Value;
        await using var connectionScope = connection.ConfigureAwait(false);
        try
        {
            var ownership = await MigrationChecks.CheckOwnership(connection, cancellationToken).ConfigureAwait(false);
            if (ownership.IsError || !ownership.Value)
            {
                return ownership.IsError ? ownership.Error : Results.Ok();
            }

            var applied = await MigrationChecks.CheckApplied(connection, migrations, cancellationToken).ConfigureAwait(false);
            return applied.IsError ? applied.Error : Results.Ok();
        }
        catch (SqliteException exception) when (SqliteErrors.TryMap(exception, "Checking the database", out var error))
        {
            return error;
        }
    }

    private static async Task Backup(SqliteConnection connection, int from, CancellationToken cancellationToken)
    {
        var backup = $"{connection.DataSource}.bak-v{from.ToString(CultureInfo.InvariantCulture)}";
        if (File.Exists(backup))
        {
            return; // the first backup of a version is the one kept
        }

        // VACUUM INTO writes one consistent copy (with the WAL's newest frames) and refuses an existing file, so it
        // writes a unique temp name; an atomic no-overwrite move then puts it in place, or loses to a racing runner's.
        var temp = $"{backup}.tmp-{Environment.ProcessId.ToString(CultureInfo.InvariantCulture)}-{Path.GetRandomFileName()}";
        try
        {
            var command = connection.CreateCommand();
            await using var commandScope = command.ConfigureAwait(false);
            command.CommandText = "VACUUM INTO $path";
            _ = command.Bind("$path", temp);
            _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            MigrationBackups.RestrictToOwner(temp); // why: VACUUM INTO writes by umask (0644) while the database itself is 0600
            try
            {
                File.Move(temp, backup, overwrite: false);
            }
            catch (IOException) when (File.Exists(backup))
            {
                // another runner's backup of the same version got there first; keep it
            }
        }
        finally
        {
            File.Delete(temp);
        }
    }

    private async Task<Result<MigrationReport>> ApplyPending(SqliteConnection connection, IReadOnlyList<Migration> migrations, int from, int target, CancellationToken cancellationToken)
    {
        var applied = new List<int>();
        foreach (var migration in migrations.Where(m => m.Version > from && m.Version <= target))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var outcome = await ApplyOne(connection, migration, cancellationToken).ConfigureAwait(false);
            if (outcome.IsError)
            {
                return outcome.Error;
            }

            if (outcome.Value)
            {
                applied.Add(migration.Version);
            }
        }

        // Re-read: a concurrent runner may have applied versions this one skipped.
        var to = (int)await Current(connection, transaction: null, cancellationToken).ConfigureAwait(false);
        return new MigrationReport(from, to, applied);
    }

    private async Task<Result<bool>> ApplyOne(SqliteConnection connection, Migration migration, CancellationToken cancellationToken)
    {
        // PRAGMA foreign_keys is a no-op inside a transaction, so it is switched off before BEGIN and back on after.
        await Run(connection, "PRAGMA foreign_keys = OFF", cancellationToken).ConfigureAwait(false);
        try
        {
            var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using var transactionScope = transaction.ConfigureAwait(false);
            // Another runner may have applied it since we read schema_version; re-check inside the write lock.
            if (await Current(connection, transaction, cancellationToken).ConfigureAwait(false) >= migration.Version)
            {
                return false;
            }

            await Run(connection, migration.Sql, cancellationToken, transaction).ConfigureAwait(false);
            var violations = await Violations(connection, transaction, cancellationToken).ConfigureAwait(false);
            if (violations.Count > 0)
            {
                return DataErrors.Constraint.With(
                    $"Migration {migration.Version.ToString(CultureInfo.InvariantCulture)} ({migration.Name}) leaves foreign-key violations ({string.Join(", ", violations)}); it was rolled back.");
            }

            await Record(connection, transaction, migration, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (SqliteException exception) when (SqliteErrors.TryMap(exception, $"Applying migration {migration.Version.ToString(CultureInfo.InvariantCulture)} ({migration.Name})", out var error))
        {
            return error;
        }
        finally
        {
            await Run(connection, "PRAGMA foreign_keys = ON", CancellationToken.None).ConfigureAwait(false);
        }
    }

    private static async Task<List<string>> Violations(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        await using var commandScope = command.ConfigureAwait(false);
        command.Transaction = transaction;
        command.CommandText = SelectViolations;
        var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await using var readerScope = reader.ConfigureAwait(false);
        var found = new List<string>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            found.Add($"{reader.GetString(0)} → {reader.GetString(1)}");
        }

        return found;
    }

    private async Task Record(SqliteConnection connection, SqliteTransaction transaction, Migration migration, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        await using var commandScope = command.ConfigureAwait(false);
        command.Transaction = transaction;
        command.CommandText = InsertApplied;
        _ = command.Bind("$v", migration.Version).Bind("$n", migration.Name).Bind("$c", migration.Checksum).Bind("$t", clock.GetUtcNow());
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<long> Current(SqliteConnection connection, SqliteTransaction? transaction, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        await using var commandScope = command.ConfigureAwait(false);
        command.Transaction = transaction;
        command.CommandText = SelectCurrent;
        return (long)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!; // why: MAX with COALESCE always returns one integer
    }

    private static async Task Run(SqliteConnection connection, string sql, CancellationToken cancellationToken, SqliteTransaction? transaction = null)
    {
        var command = connection.CreateCommand();
        await using var commandScope = command.ConfigureAwait(false);
        command.Transaction = transaction;
#pragma warning disable CA2100 // the callers pass the embedded, numbered migration scripts and fixed pragmas, never input
        command.CommandText = sql;
#pragma warning restore CA2100
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
