namespace Aw.Sindri.Core.Scheduling;

/// <summary>Sindri's own SQLite stores (the ledger and views.db): a periodic checkpoint of each write-ahead log (#2221).</summary>
public interface IStoreCheckpoints
{
    /// <summary>Checkpoints and truncates each store's log, waiting only briefly for a reader; a store held by a reader stays partial, never an error.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>One entry per store that exists.</returns>
    public ValueTask<IReadOnlyList<StoreCheckpoint>> Run(CancellationToken cancellationToken);
}
