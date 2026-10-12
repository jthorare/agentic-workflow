using Aw.Kernel;
using Aw.Sindri.Core.Lease;

namespace Aw.Sindri.Core.Ledger;

/// <summary>
/// The ledger (port; factory addendum §17.20): an append-only, idempotent event log. Every write is fenced by the
/// writer's lease grant. SQLite today; a server database later, passing the same contract suite.
/// </summary>
public interface ILedgerStore
{
    /// <summary>Records <paramref name="entry"/> if the grant's epoch is current and its key is new, in one transaction.</summary>
    /// <param name="entry">The event.</param>
    /// <param name="grant">The writer's lease grant.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The position (with AlreadyRecorded on a replay), or SND-LEASE-003 when the epoch isn't current.</returns>
    public ValueTask<Result<LedgerAppend>> Append(LedgerEntry entry, LeaseGrant grant, CancellationToken cancellationToken);

    /// <summary>Events after <paramref name="sequence"/>, oldest first, at most <paramref name="max"/>.</summary>
    /// <param name="sequence">The cursor (0 for the start).</param>
    /// <param name="max">The most events to return, at least 1.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The events.</returns>
    public ValueTask<Result<IReadOnlyList<LedgerEvent>>> ReadAfter(long sequence, int max, CancellationToken cancellationToken);

    /// <summary>The most recent event of <paramref name="type"/>, or null.</summary>
    /// <param name="type">The event type.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The event, if any.</returns>
    public ValueTask<Result<LedgerEvent?>> Latest(string type, CancellationToken cancellationToken);

    /// <summary>The schema state of the store against this build, without changing anything.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Applied and expected migration counts.</returns>
    public ValueTask<Result<SchemaStatus>> Schema(CancellationToken cancellationToken);
}
