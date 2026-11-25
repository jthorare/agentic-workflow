using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Tests.CreateItem;

/// <summary>A ledger that refuses 'item.linked' and passes every other entry to <paramref name="inner"/>.</summary>
/// <param name="inner">The ledger behind it.</param>
internal sealed class LinkRefusingLedger(ILedgerStore inner) : ILedgerStore
{
    public ValueTask<Result<LedgerAppend>> Append(LedgerEntry entry, LeaseGrant grant, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return entry.Envelope.Type == EventTypes.ItemLinked
            ? ValueTask.FromResult<Result<LedgerAppend>>(SindriErrors.LedgerUnavailable.With("ledger.db: database or disk is full"))
            : inner.Append(entry, grant, cancellationToken);
    }

    public ValueTask<Result<IReadOnlyList<LedgerEvent>>> ReadAfter(long sequence, int max, CancellationToken cancellationToken) => inner.ReadAfter(sequence, max, cancellationToken);

    public ValueTask<Result<LedgerEvent?>> Latest(string type, CancellationToken cancellationToken) => inner.Latest(type, cancellationToken);

    public ValueTask<Result<long>> Head(CancellationToken cancellationToken) => inner.Head(cancellationToken);

    public ValueTask<Result<SchemaStatus>> Schema(CancellationToken cancellationToken) => inner.Schema(cancellationToken);
}
