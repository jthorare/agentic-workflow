using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Tests.Views;

/// <summary>A ledger reader whose head or page reads can be made to fail with SND-LEDGER-002.</summary>
internal sealed class FlakyLedgerReader(ILedgerReader inner) : ILedgerReader
{
    public bool FailHead { get; set; }

    public bool FailReads { get; set; }

    public ValueTask<Result<IReadOnlyList<LedgerEvent>>> ReadAfter(long sequence, int max, CancellationToken cancellationToken) =>
        FailReads ? ValueTask.FromResult<Result<IReadOnlyList<LedgerEvent>>>(Unavailable) : inner.ReadAfter(sequence, max, cancellationToken);

    public ValueTask<Result<LedgerScan>> Scan(long sequence, int max, LedgerFilter filter, CancellationToken cancellationToken) =>
        FailReads ? ValueTask.FromResult<Result<LedgerScan>>(Unavailable) : inner.Scan(sequence, max, filter, cancellationToken);

    public ValueTask<Result<LedgerEvent?>> Latest(string type, CancellationToken cancellationToken) => inner.Latest(type, cancellationToken);

    public ValueTask<Result<LedgerEvent?>> ByKey(string idempotencyKey, CancellationToken cancellationToken) => inner.ByKey(idempotencyKey, cancellationToken);

    public ValueTask<Result<long>> Head(CancellationToken cancellationToken) =>
        FailHead ? ValueTask.FromResult<Result<long>>(Unavailable) : inner.Head(cancellationToken);

    private static Error Unavailable => SindriErrors.LedgerUnavailable.With("the ledger can't be read (fixture)");
}
