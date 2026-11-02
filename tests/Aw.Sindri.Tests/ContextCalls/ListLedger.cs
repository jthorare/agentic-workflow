using Aw.Kernel;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>A read-only ledger over a fixed list of events.</summary>
internal sealed class ListLedger(LedgerEvent[] events) : ILedgerStore, ILedgerReader
{
    public ValueTask<Result<LedgerAppend>> Append(LedgerEntry entry, LeaseGrant grant, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<IReadOnlyList<LedgerEvent>>> ReadAfter(long sequence, int max, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Results.Success<IReadOnlyList<LedgerEvent>>([.. events.Where(e => e.Sequence > sequence).Take(max)]));

    public ValueTask<Result<LedgerEvent?>> Latest(string type, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<LedgerEvent?>> ByKey(string idempotencyKey, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<long>> Head(CancellationToken cancellationToken) => ValueTask.FromResult(Results.Success(events.Length == 0 ? 0 : events.Max(static e => e.Sequence)));

    public ValueTask<Result<SchemaStatus>> Schema(CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<LedgerScan>> Scan(long sequence, int max, LedgerFilter filter, CancellationToken cancellationToken) => throw new NotSupportedException();
}
