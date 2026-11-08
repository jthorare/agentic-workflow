using Aw.Sindri.Core.Scheduling;
using Microsoft.Data.Sqlite;

namespace Aw.Sindri.Infrastructure.Ledger;

/// <summary>
/// The checkpoint of ledger.db and views.db (#2221): wal_checkpoint(TRUNCATE) on a non-pooled connection of its own, waiting up to two seconds for a
/// reader. A store a reader still holds after that is reported as not truncated, never as an error; a store that can't be opened is skipped until the next tick.
/// </summary>
/// <param name="paths">The store files to checkpoint.</param>
internal sealed class SqliteStoreCheckpoints(IReadOnlyList<string> paths) : IStoreCheckpoints
{
    private const int WaitSeconds = 2;

    public ValueTask<IReadOnlyList<StoreCheckpoint>> Run(CancellationToken cancellationToken)
    {
        var done = new List<StoreCheckpoint>();
        foreach (var path in paths.Where(static p => File.Exists(p)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Checkpoint(path) is { } result)
            {
                done.Add(result);
            }
        }

        return ValueTask.FromResult<IReadOnlyList<StoreCheckpoint>>(done);
    }

    private static StoreCheckpoint? Checkpoint(string path)
    {
        var name = Path.GetFileName(path);
        try
        {
            var wal = new FileInfo(path + "-wal");
            var before = wal.Exists ? wal.Length : 0;
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadWrite, Pooling = false, DefaultTimeout = WaitSeconds }.ToString());
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA wal_checkpoint(TRUNCATE)";   // DefaultTimeout above is the wait for a reader
            using var reader = command.ExecuteReader();
            do
            {
                if (reader.FieldCount == 3 && reader.Read())
                {
                    return new StoreCheckpoint(name, reader.GetInt64(0) == 0, before);
                }
            }
            while (reader.NextResult());
            return new StoreCheckpoint(name, Truncated: false, before);
        }
        catch (SqliteException)
        {
            return new StoreCheckpoint(name, Truncated: false, LogBytes: 0);   // a reader or writer in the way: partial, the next tick tries again
        }
    }
}
