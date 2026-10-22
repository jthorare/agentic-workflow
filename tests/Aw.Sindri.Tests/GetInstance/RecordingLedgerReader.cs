using Aw.Kernel;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Tests.GetInstance;

/// <summary>A ledger reader that counts the rows each read hands back, so a test can say a read touched only the rows it wanted.</summary>
internal sealed class RecordingLedgerReader(ILedgerReader inner) : ILedgerReader
{
    public int RowsRead { get; private set; }

    public async ValueTask<Result<IReadOnlyList<LedgerEvent>>> ReadAfter(long sequence, int max, CancellationToken cancellationToken)
    {
        var rows = await inner.ReadAfter(sequence, max, cancellationToken).ConfigureAwait(false);
        RowsRead += rows.IsSuccess ? rows.Value.Count : 0;
        return rows;
    }

    public async ValueTask<Result<LedgerScan>> Scan(long sequence, int max, LedgerFilter filter, CancellationToken cancellationToken)
    {
        var scan = await inner.Scan(sequence, max, filter, cancellationToken).ConfigureAwait(false);
        RowsRead += scan.IsSuccess ? scan.Value.Events.Count : 0;
        return scan;
    }

    public ValueTask<Result<LedgerEvent?>> Latest(string type, CancellationToken cancellationToken) => inner.Latest(type, cancellationToken);

    public ValueTask<Result<long>> Head(CancellationToken cancellationToken) => inner.Head(cancellationToken);
}
