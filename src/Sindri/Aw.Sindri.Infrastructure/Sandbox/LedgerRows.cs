using Aw.Kernel;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>Reads every ledger row of one type, page by page (the sandbox reads that go to the ledger itself and never to views.db, whose projector may lag).</summary>
internal static class LedgerRows
{
    private const int Page = 500;

    /// <summary>Every row of <paramref name="type"/>, oldest first.</summary>
    /// <param name="ledger">The ledger, read-only.</param>
    /// <param name="type">The event type.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The rows, or the ledger's error.</returns>
    internal static async ValueTask<Result<List<LedgerEvent>>> Of(ILedgerReader ledger, string type, CancellationToken cancellationToken)
    {
        var filter = new LedgerFilter(TypeFilter.Parse(type).Value, Item: null);
        var rows = new List<LedgerEvent>();
        var cursor = 0L;
        while (true)
        {
            var scan = await ledger.Scan(cursor, Page, filter, cancellationToken).ConfigureAwait(false);
            if (scan.IsError)
            {
                return scan.Error;
            }

            rows.AddRange(scan.Value.Events);
            if (scan.Value.ScannedTo <= cursor)
            {
                return rows;
            }

            cursor = scan.Value.ScannedTo;
        }
    }
}
