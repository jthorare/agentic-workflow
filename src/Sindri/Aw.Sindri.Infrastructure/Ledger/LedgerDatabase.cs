using Aw.Kernel;
using Aw.Kernel.Data;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Infrastructure.Ledger;

/// <summary>
/// The ledger file: its connections, its migrations (events, lease, outbox_commands, recipes, workflow_claims) and its permissions. Writers call
/// <see cref="Writable"/>, which migrates once per process; readers never migrate or create the file.
/// </summary>
internal sealed class LedgerDatabase : IDisposable
{
    private readonly SindriSettings _settings;
    private readonly MigrationRunner _runner;
    private readonly SemaphoreSlim _migrate = new(1, 1);
    private bool _migrated;

    public LedgerDatabase(SindriSettings settings, TimeProvider clock)
    {
        _settings = settings;
        var connections = new SqliteConnectionFactory(new SqliteOptions(settings.LedgerPath));
        Sessions = new SqliteUnitOfWorkFactory(connections);
        _runner = new MigrationRunner(connections, clock);
    }

    /// <inheritdoc/>
    public void Dispose() => _migrate.Dispose();

    /// <summary>Opens write and read sessions on the file.</summary>
    public SqliteUnitOfWorkFactory Sessions { get; }

    /// <summary>Every migration this build ships, in order.</summary>
    public static IReadOnlyList<Migration> Migrations { get; } = LoadMigrations();

    /// <summary>Migrates the file once per process (creating it, 0600 in a 0700 home), then returns the session factory.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The factory, or SND-LEDGER-001/002 when the file can't be brought up to date.</returns>
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
                OwnerOnly.EnsureDirectory(_settings.Home);
                var report = await _runner.Migrate(Migrations, cancellationToken).ConfigureAwait(false);
                if (report.IsError)
                {
                    return Translate(report.Error);
                }

                OwnerOnly.RestrictFiles(_settings.LedgerPath, $"{_settings.LedgerPath}-wal", $"{_settings.LedgerPath}-shm");
                _migrated = true;
            }

            return Sessions;
        }
        finally
        {
            _ = _migrate.Release();
        }
    }

    /// <summary>The applied migration count against this build's, read-only (a missing file has none applied).</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The status.</returns>
    public async ValueTask<Result<SchemaStatus>> Schema(CancellationToken cancellationToken)
    {
        if (!File.Exists(_settings.LedgerPath))
        {
            return new SchemaStatus(0, Migrations.Count);
        }

        var read = await Sessions.BeginRead(cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return Translate(read.Error);
        }

        await using (read.Value.ConfigureAwait(false))
        {
            var applied = await SqliteQueries.One(read.Value, new SqlStatement(LedgerSql.CountApplied), (IRowMapper<int>)CountMapper.Instance, cancellationToken)
                .ConfigureAwait(false);
            return applied.IsError ? Translate(applied.Error) : new SchemaStatus(applied.Value, Migrations.Count);
        }
    }

    /// <summary>Maps a store failure to Sindri's catalogue: schema skew is SND-LEDGER-001, anything else SND-LEDGER-002.</summary>
    /// <param name="error">The Kernel.Data error.</param>
    /// <returns>The Sindri error, keeping the original code in the message.</returns>
    public static Error Translate(Error error) => error.Code switch
    {
        "AW-DATA-005" or "AW-DATA-006" or "AW-DATA-007" or "AW-DATA-009" => SindriErrors.LedgerSchema.With($"{error.Code}: {error.Message}"),
        _ => SindriErrors.LedgerUnavailable.With($"{error.Code}: {error.Message}"),
    };

    /// <summary>
    /// The outbox table's version (engine spec §7.2, S1-D9): pinned after S1's two files, so a new file never renumbers
    /// it and Sindri's own files are 4 and up. A file numbered 3 collides with it and the runner refuses the set.
    /// </summary>
    internal const int OutboxVersion = 3;

    /// <summary>The first migration with the recipe registry (engine spec §4.1); reads of an older file find no recipes.</summary>
    internal const int RecipesVersion = 4;

    /// <summary>The first migration with 'workflow_claims' (engine spec §5.9; engine P6 unit (d)); reads of an older file find no claim.</summary>
    internal const int ClaimsVersion = 6;

    /// <summary>The first migration with 'pr_comment_marks' (PR babysitter spec §5.4, W26); reads of an older file find no mark.</summary>
    internal const int PrCommentMarksVersion = 7;

    private static IReadOnlyList<Migration> LoadMigrations()
    {
        var own = EmbeddedMigrations.Load(typeof(LedgerDatabase).Assembly, "Aw.Sindri.Infrastructure.Ledger.Migrations.").Value;
        return [.. own.Where(static m => m.Version < OutboxVersion), SqliteOutboxSchema.CreateMigration(OutboxVersion), .. own.Where(static m => m.Version >= OutboxVersion)];
    }
}
