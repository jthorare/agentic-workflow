using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Infrastructure.Ledger;

/// <summary>The checkpoint of ledger.db and views.db (#2221).</summary>
/// <param name="paths">The store files to checkpoint.</param>
internal sealed class SqliteStoreCheckpoints(IReadOnlyList<string> paths) : IStoreCheckpoints
{
    public ValueTask<IReadOnlyList<StoreCheckpoint>> Run(CancellationToken cancellationToken) =>
        throw new NotSupportedException($"{paths.Count} stores");
}
