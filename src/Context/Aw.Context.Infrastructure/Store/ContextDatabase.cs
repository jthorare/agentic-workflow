using Aw.Context.Core;
using Aw.Kernel;
using Aw.Kernel.Data;

namespace Aw.Context.Infrastructure.Store;

/// <summary>
/// The engine's store, context.db (spec §6.2): its connections, its migrations and its permissions (directory 0700,
/// the file and its -wal and -shm 0600). Writers call <see cref="Writable"/>, which migrates once per process; readers
/// call <see cref="Readable"/>, which never creates the file.
/// </summary>
internal sealed class ContextDatabase : IDisposable
{
    private readonly ContextStoreSettings _settings;
    private readonly MigrationRunner _runner;
    private readonly SemaphoreSlim _migrate = new(1, 1);
    private bool _migrated;

    public ContextDatabase(ContextStoreSettings settings, TimeProvider clock)
    {
        _settings = settings;
        var connections = new SqliteConnectionFactory(new SqliteOptions(settings.DatabasePath));
        Sessions = new SqliteUnitOfWorkFactory(connections);
        _runner = new MigrationRunner(connections, clock);
    }

    /// <summary>Every migration this build ships, in order.</summary>
    public static IReadOnlyList<Migration> Migrations { get; } =
        EmbeddedMigrations.Load(typeof(ContextDatabase).Assembly, "Aw.Context.Infrastructure.Store.Migrations.").Value;

    /// <summary>The session factory (the graph store's reads open it as it is and never migrate; other reads go through <see cref="Readable"/> first).</summary>
    public SqliteUnitOfWorkFactory Sessions { get; }

    /// <inheritdoc/>
    public void Dispose() => _migrate.Dispose();

    /// <summary>Migrates the file once per process (creating it in a 0700 directory), then returns the session factory.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The factory, or SND-CONTEXT-004 when the store can't be brought up to date.</returns>
    public async ValueTask<Result<SqliteUnitOfWorkFactory>> Writable(CancellationToken cancellationToken)
    {
        if (_migrated)
        {
            return Sessions;
        }

        await _migrate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_migrated)
            {
                if (Prepare() is { } unusable)
                {
                    return unusable;
                }

                var report = await _runner.Migrate(Migrations, cancellationToken).ConfigureAwait(false);
                if (report.IsError)
                {
                    return Translate(report.Error);
                }

                RestrictFiles();
                _migrated = true;
            }

            return Sessions;
        }
        finally
        {
            _ = _migrate.Release();
        }
    }

    /// <summary>The session factory when the store exists (migrated by this build), else null: nothing was ever refreshed.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The factory, null for no store yet, or SND-CONTEXT-004.</returns>
    public async ValueTask<Result<SqliteUnitOfWorkFactory?>> Readable(CancellationToken cancellationToken)
    {
        if (!File.Exists(_settings.DatabasePath))
        {
            return (SqliteUnitOfWorkFactory?)null;
        }

        var writable = await Writable(cancellationToken).ConfigureAwait(false);   // an older schema is migrated on first use (spec §8.6, SND-CONTEXT-004 fix)
        return writable.IsError ? writable.Error : writable.Value;
    }

    /// <summary>Creates the 0700 home; a home that can't be made or chmod-ed is SND-CONTEXT-004, never an exception a query sees.</summary>
    private Error? Prepare()
    {
        try
        {
            OwnerOnly.EnsureDirectory(_settings.Home);
            return null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return ContextErrors.StoreUnavailable.With($"the context home can't be created or restricted ({e.GetType().Name}); check $AW_SINDRI_HOME permissions");
        }
    }

    /// <summary>Restricts the store's files to the owner (the -wal and -shm files appear after the first write).</summary>
    public void RestrictFiles()
    {
        try
        {
            OwnerOnly.RestrictFiles(_settings.DatabasePath, _settings.DatabasePath + "-wal", _settings.DatabasePath + "-shm");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // the 0700 home is the guard; the 0600 file modes are defence in depth, and a filesystem that refuses chmod keeps the umask modes
        }
    }

    /// <summary>Truncates the write-ahead log, so old page images of deleted rows don't stay in it (spec §4.5). Best effort: a reader holding the log makes the checkpoint partial, never an error.</summary>
    public void TruncateWal()
    {
        try
        {
            using var connection = new Microsoft.Data.Sqlite.SqliteConnection(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = _settings.DatabasePath, Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadWrite, Pooling = false }.ToString());
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA wal_checkpoint(TRUNCATE)";
            _ = command.ExecuteScalar();
        }
        catch (Microsoft.Data.Sqlite.SqliteException)
        {
            // the rows are already deleted and committed; a checkpoint that can't run now runs with the next one
        }
    }

    /// <summary>Maps a store failure to SND-CONTEXT-004, keeping the original code in the message.</summary>
    /// <param name="error">The Kernel.Data error.</param>
    /// <returns>The context error.</returns>
    public static Error Translate(Error error) =>
        ContextErrors.StoreUnavailable.With($"{error.Code}: {error.Message} (never delete context.db; check $AW_SINDRI_HOME permissions)");
}
