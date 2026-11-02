using Aw.Kernel;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Tests.Views;

/// <summary>A ledger reader that records the page size of every ReadAfter.</summary>
internal sealed class CountingLedgerReader(ILedgerReader inner) : ILedgerReader
{
    private readonly List<int> _pages = [];

    public IReadOnlyList<int> Pages => _pages;

    public ValueTask<Result<IReadOnlyList<LedgerEvent>>> ReadAfter(long sequence, int max, CancellationToken cancellationToken)
    {
        _pages.Add(max);
        return inner.ReadAfter(sequence, max, cancellationToken);
    }

    public ValueTask<Result<LedgerScan>> Scan(long sequence, int max, LedgerFilter filter, CancellationToken cancellationToken) =>
        inner.Scan(sequence, max, filter, cancellationToken);

    public ValueTask<Result<LedgerEvent?>> Latest(string type, CancellationToken cancellationToken) => inner.Latest(type, cancellationToken);

    public ValueTask<Result<LedgerEvent?>> ByKey(string idempotencyKey, CancellationToken cancellationToken) => inner.ByKey(idempotencyKey, cancellationToken);

    public ValueTask<Result<long>> Head(CancellationToken cancellationToken) => inner.Head(cancellationToken);
}
