using Aw.Kernel;
using Aw.Kernel.Data;
using Aw.Sindri.Core;

namespace Aw.Sindri.Infrastructure.Views;

/// <summary>
/// The views file (API spec §2.3): its connections, its migrations and its permissions, like the ledger's. The projector
/// (the only writer) calls <see cref="Writable"/>, which creates and migrates it once per process; readers never do.
/// </summary>
internal sealed class ViewsDatabase : IDisposable
{
    private readonly SindriSettings _settings;
    private readonly MigrationRunner _runner;
    private readonly SemaphoreSlim _migrate = new(1, 1);
    private bool _migrated;

    public ViewsDatabase(SindriSettings settings, TimeProvider clock)
    {
        _settings = settings;
        var connections = new SqliteConnectionFactory(new SqliteOptions(settings.ViewsPath));
        Sessions = new SqliteUnitOfWorkFactory(connections);
        _runner = new MigrationRunner(connections, clock, backupBeforeUpgrade: false);   // derived data: a rebuild replaces a backup
    }

    /// <inheritdoc/>
    public void Dispose() => _migrate.Dispose();

    /// <summary>Opens write and read sessions on the file.</summary>
    public SqliteUnitOfWorkFactory Sessions { get; }

    /// <summary>True when views.db exists.</summary>
    public bool Exists => File.Exists(_settings.ViewsPath);

    /// <summary>Every migration this build ships, in order.</summary>
    public static IReadOnlyList<Migration> Migrations { get; } =
        EmbeddedMigrations.Load(typeof(ViewsDatabase).Assembly, "Aw.Sindri.Infrastructure.Views.Migrations.").Value;

    /// <summary>Migrates the file once per process (creating it, 0600 in a 0700 home), then returns the session factory.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The factory, or SND-VIEW-001 when the file can't be brought up to date.</returns>
    public async ValueTask<Result<SqliteUnitOfWorkFactory>> Writable(CancellationToken cancellationToken)
    {
        if (_migrated && Exists)
        {
            return Sessions;
        }

        await _migrate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_migrated || !Exists)
            {
                OwnerOnly.EnsureDirectory(_settings.Home);
                var report = await _runner.Migrate(Migrations, cancellationToken).ConfigureAwait(false);
                if (report.IsError)
                {
                    return Translate(report.Error);
                }

                OwnerOnly.RestrictFiles(_settings.ViewsPath, $"{_settings.ViewsPath}-wal", $"{_settings.ViewsPath}-shm");
                _migrated = true;
            }

            return Sessions;
        }
        finally
        {
            _ = _migrate.Release();
        }
    }

    /// <summary>
    /// Stops <paramref name="connection"/> from checkpointing the WAL when it closes. The projector opens and closes a writer per page, and it is
    /// usually the only connection, so each close copied the whole log into views.db and synced it: about a second of a 100k catch-up's
    /// 100 pages, three under disk load (#2890). The log is still checkpointed as it grows (wal_autocheckpoint, inside a commit) and
    /// truncated to journal_size_limit; a log left at exit is replayed on the next open.
    /// </summary>
    /// <param name="connection">An open read-write connection to views.db.</param>
    public static void NoCheckpointOnClose(Microsoft.Data.Sqlite.SqliteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        _ = SQLitePCL.raw.sqlite3_db_config(connection.Handle, SQLitePCL.raw.SQLITE_DBCONFIG_NO_CKPT_ON_CLOSE, 1, out _);
    }

    /// <summary>Maps a store failure to Sindri's catalogue: views.db that can't be used stalls the read side (SND-VIEW-001).</summary>
    /// <param name="error">The Kernel.Data error.</param>
    /// <returns>The Sindri error, keeping the original code in the message.</returns>
    public static Error Translate(Error error) => SindriErrors.ViewsStalled.With($"views.db: {error.Code}: {error.Message}");
}
