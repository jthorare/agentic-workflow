using Aw.Kernel;
using Aw.Kernel.Data;
using Aw.Sindri.Core;

namespace Aw.Sindri.Infrastructure.Messages;

/// <summary>
/// The messages.db file (messages spec §8.1, §11): its connections, its own migration sequence (Messages/Migrations, from 0001) and its permissions, 0600
/// in the 0700 home. Unlike views.db it is not derived from the ledger, but it takes no backup before an upgrade either: a 'messages.db.bak-vN' copy would
/// hold message text that retention and purge never reach (§11 "Where message text lives"), and the cache part comes back with a re-sync.
/// </summary>
internal sealed class MessagesDatabase : IDisposable
{
    private readonly SindriSettings _settings;
    private readonly MigrationRunner _runner;
    private readonly SemaphoreSlim _migrate = new(1, 1);
    private bool _migrated;

    public MessagesDatabase(SindriSettings settings, TimeProvider clock)
    {
        _settings = settings;
        var connections = new SqliteConnectionFactory(new SqliteOptions(settings.MessagesPath));
        Sessions = new SqliteUnitOfWorkFactory(connections);
        _runner = new MigrationRunner(connections, clock, backupBeforeUpgrade: false);
    }

    /// <inheritdoc/>
    public void Dispose() => _migrate.Dispose();

    /// <summary>Opens write and read sessions on the file.</summary>
    public SqliteUnitOfWorkFactory Sessions { get; }

    /// <summary>True when messages.db exists.</summary>
    public bool Exists => File.Exists(_settings.MessagesPath);

    /// <summary>The file's path.</summary>
    public string Path => _settings.MessagesPath;

    /// <summary>Every migration this build ships, in order.</summary>
    public static IReadOnlyList<Migration> Migrations { get; } =
        EmbeddedMigrations.Load(typeof(MessagesDatabase).Assembly, "Aw.Sindri.Infrastructure.Messages.Migrations.").Value;

    /// <summary>Migrates the file once per process (creating it, 0600 in a 0700 home), then returns the session factory.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The factory, or SND-MSG-025 when the file can't be brought up to date.</returns>
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

                OwnerOnly.RestrictFiles(_settings.MessagesPath, $"{_settings.MessagesPath}-wal", $"{_settings.MessagesPath}-shm");
                _migrated = true;
            }

            return Sessions;
        }
        finally
        {
            _ = _migrate.Release();
        }
    }

    /// <summary>Maps a store failure to Sindri's catalogue: a messages.db that can't be used is SND-MSG-025.</summary>
    /// <param name="error">The Kernel.Data error.</param>
    /// <returns>The Sindri error, keeping the original code in the message.</returns>
    public static Error Translate(Error error) => SindriErrors.MessagesStoreUnusable.With($"messages.db: {error.Code}: {error.Message}");
}
