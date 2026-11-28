using Aw.Judge.Core.Batching;
using Aw.Judge.Core.Ports;
using Aw.Judge.Infrastructure.Sqlite;
using Aw.Kernel.Data;
using Microsoft.Data.Sqlite;

namespace Aw.Judge.Infrastructure.Traits;

/// <summary>
/// ITraitVerdictCache over '&lt;judge dir&gt;/trait-check/verdicts.sqlite' (WAL, its own embedded migrations, no backup
/// before an upgrade: it is a cache). A read skips rows older than <paramref name="keep"/>; a write replaces its keys,
/// then deletes the expired rows and every row past the newest <paramref name="maxEntries"/>. Any failure (the file
/// can't be made, a busy lock past the timeout, a bad row) is a miss or a dropped write, never an exception.
/// </summary>
/// <param name="path">The database file.</param>
/// <param name="clock">When a verdict was stored and how old one may be.</param>
/// <param name="keep">How long a verdict is used.</param>
/// <param name="maxEntries">The most verdicts kept.</param>
internal sealed class SqliteTraitVerdictCache(string path, TimeProvider clock, TimeSpan keep, int maxEntries) : ITraitVerdictCache
{
    internal const string FileName = "verdicts.sqlite";
    internal const string ResourcePrefix = "Aw.Judge.Infrastructure.Sqlite.VerdictCacheMigrations.";
    internal static readonly TimeSpan DefaultKeep = TimeSpan.FromDays(7);
    internal const int DefaultMaxEntries = 100_000;

    // Two hooks may write at once; a cache waits briefly, then drops its write.
    private static readonly TimeSpan _busyTimeout = TimeSpan.FromMilliseconds(500);

    private readonly SqliteOptions _options = new(path) { BusyTimeout = _busyTimeout };
    private bool _ready;

    /// <inheritdoc />
    public async ValueTask<IReadOnlyDictionary<string, ChoiceBatchAnswer>> Get(IReadOnlyCollection<string> keys, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keys);
        Dictionary<string, ChoiceBatchAnswer> found = new(StringComparer.Ordinal);
        if (keys.Count == 0)
        {
            return found;
        }

        try
        {
            if (!await Ready(cancellationToken).ConfigureAwait(false)
                || await new SqliteUnitOfWorkFactory(new SqliteConnectionFactory(_options)).BeginRead(cancellationToken).ConfigureAwait(false) is not { IsSuccess: true } begun)
            {
                return found;
            }

            await using (begun.Value.ConfigureAwait(false))
            {
                var cutoff = Cutoff();
                foreach (var key in keys.Distinct(StringComparer.Ordinal))
                {
                    var rows = await SqliteQueries.List(begun.Value, VerdictSql.Lookup(key, cutoff), VerdictMapper.Instance, cancellationToken).ConfigureAwait(false);
                    if (rows is { IsSuccess: true, Value: [var verdict, ..] })
                    {
                        found[key] = verdict;
                    }
                }
            }

            return found;
        }
        catch (Exception e) when (Unusable(e))
        {
            return found;
        }
    }

    /// <inheritdoc />
    public async ValueTask Put(IReadOnlyDictionary<string, ChoiceBatchAnswer> verdicts, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verdicts);
        try
        {
            if (verdicts.Count == 0
                || !await Ready(cancellationToken).ConfigureAwait(false)
                || await new SqliteUnitOfWorkFactory(new SqliteConnectionFactory(_options)).BeginSession(cancellationToken).ConfigureAwait(false) is not { IsSuccess: true } begun)
            {
                return;
            }

            await using (begun.Value.ConfigureAwait(false))
            {
                var now = clock.GetUtcNow().ToUnixTimeMilliseconds();
                List<SqlStatement> statements = [.. verdicts.Select(pair => VerdictSql.Store(pair.Key, pair.Value, now)), VerdictSql.ExpireBefore(Cutoff()), VerdictSql.KeepNewest(maxEntries)];
                foreach (var statement in statements)
                {
                    if ((await SqliteQueries.Execute(begun.Value, statement, cancellationToken).ConfigureAwait(false)).IsError)
                    {
                        return;
                    }
                }

                _ = await begun.Value.Commit(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception e) when (Unusable(e))
        {
            // The cache is best effort: the verdict is asked again next time.
        }
    }

    private static bool Unusable(Exception e) => e is SqliteException or StoredRowException or InvalidDataException or IOException or UnauthorizedAccessException;

    private long Cutoff() => (clock.GetUtcNow() - keep).ToUnixTimeMilliseconds();

    // The directory, then the schema, once per process.
    private async ValueTask<bool> Ready(CancellationToken cancellationToken)
    {
        if (_ready)
        {
            return true;
        }

        if (Path.GetDirectoryName(Path.GetFullPath(path)) is { } directory)
        {
            _ = Directory.CreateDirectory(directory);
        }

        var migrations = EmbeddedMigrations.Load(typeof(SqliteTraitVerdictCache).Assembly, ResourcePrefix);
        if (migrations.IsError)
        {
            return false;
        }

        var migrated = await new MigrationRunner(new SqliteConnectionFactory(_options), clock, backupBeforeUpgrade: false).Migrate(migrations.Value, cancellationToken).ConfigureAwait(false);
        _ready = migrated.IsSuccess;
        return _ready;
    }
}
