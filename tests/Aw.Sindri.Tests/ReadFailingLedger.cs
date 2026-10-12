using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Tests;

/// <summary>A ledger whose reads fail, as a busy or unreadable file would; appends and the schema go to <paramref name="inner"/>.</summary>
/// <param name="inner">The ledger that holds the events.</param>
internal sealed class ReadFailingLedger(ILedgerStore inner) : ILedgerStore
{
    public static Error Failure { get; } = SindriErrors.LedgerUnavailable.With("ledger.db: database is locked");

    public ValueTask<Result<LedgerAppend>> Append(LedgerEntry entry, LeaseGrant grant, CancellationToken cancellationToken) =>
        inner.Append(entry, grant, cancellationToken);

    public ValueTask<Result<IReadOnlyList<LedgerEvent>>> ReadAfter(long sequence, int max, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<IReadOnlyList<LedgerEvent>>>(Failure);

    public ValueTask<Result<LedgerEvent?>> Latest(string type, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<LedgerEvent?>>(Failure);

    public ValueTask<Result<SchemaStatus>> Schema(CancellationToken cancellationToken) => inner.Schema(cancellationToken);
}
