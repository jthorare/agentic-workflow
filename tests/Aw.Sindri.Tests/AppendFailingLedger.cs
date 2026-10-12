using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Tests;

/// <summary>A ledger whose appends fail, as a full disk would; reads and the schema go to <paramref name="inner"/>.</summary>
/// <param name="inner">The ledger behind it.</param>
internal sealed class AppendFailingLedger(ILedgerStore inner) : ILedgerStore
{
    public static Error Failure { get; } = SindriErrors.LedgerUnavailable.With("ledger.db: database or disk is full");

    public ValueTask<Result<LedgerAppend>> Append(LedgerEntry entry, LeaseGrant grant, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<LedgerAppend>>(Failure);

    public ValueTask<Result<IReadOnlyList<LedgerEvent>>> ReadAfter(long sequence, int max, CancellationToken cancellationToken) =>
        inner.ReadAfter(sequence, max, cancellationToken);

    public ValueTask<Result<LedgerEvent?>> Latest(string type, CancellationToken cancellationToken) => inner.Latest(type, cancellationToken);

    public ValueTask<Result<SchemaStatus>> Schema(CancellationToken cancellationToken) => inner.Schema(cancellationToken);
}
