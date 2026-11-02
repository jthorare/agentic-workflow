using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Tests.Server;

/// <summary>A ledger reader that reads <paramref name="inner"/> until a test makes reads fail, as a locked or broken ledger file would.</summary>
internal sealed class SwitchableLedgerReader(ILedgerReader inner) : ILedgerReader
{
    /// <summary>Whether every read answers an error.</summary>
    public bool Failing { get; set; }

    public ValueTask<Result<IReadOnlyList<LedgerEvent>>> ReadAfter(long sequence, int max, CancellationToken cancellationToken) =>
        Failing ? Failed<IReadOnlyList<LedgerEvent>>() : inner.ReadAfter(sequence, max, cancellationToken);

    public ValueTask<Result<LedgerScan>> Scan(long sequence, int max, LedgerFilter filter, CancellationToken cancellationToken) =>
        Failing ? Failed<LedgerScan>() : inner.Scan(sequence, max, filter, cancellationToken);

    public ValueTask<Result<LedgerEvent?>> Latest(string type, CancellationToken cancellationToken) =>
        Failing ? Failed<LedgerEvent?>() : inner.Latest(type, cancellationToken);

    public ValueTask<Result<LedgerEvent?>> ByKey(string idempotencyKey, CancellationToken cancellationToken) =>
        Failing ? Failed<LedgerEvent?>() : inner.ByKey(idempotencyKey, cancellationToken);

    public ValueTask<Result<long>> Head(CancellationToken cancellationToken) =>
        Failing ? Failed<long>() : inner.Head(cancellationToken);

    private static ValueTask<Result<T>> Failed<T>() => ValueTask.FromResult<Result<T>>(SindriErrors.LedgerUnavailable.With("the ledger can't be read"));
}
