using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Text;

namespace Aw.Sindri.Core.Items;

/// <summary>
/// What the current caller sees of a ledger row, for the stream and the ledger read (API spec §3.1, §5.2): the
/// <see cref="PublicEvents"/> form (scrubbed and escaped). Every caller, a session too, sees every row.
/// </summary>
/// <param name="scrubber">Masks secrets in every string a payload sends.</param>
/// <param name="json">The package's AwJson options, for the public payloads.</param>
/// <param name="ledger">The ledger, scanned a page at a time.</param>
public sealed class CallerEventView(ITextScrubber scrubber, JsonSerializerOptions json, ILedgerReader ledger)
{
    /// <summary>
    /// The events after <paramref name="query"/>'s cursor, oldest first, at most the query's page size,
    /// and how far the read looked (rows it skipped move the cursor too). It scans on, page by page, while the query's own test dropped rows from a
    /// full page. When a row can't be made public it keeps what it has and the cursor stays before the row; with nothing kept that
    /// is the error.
    /// </summary>
    /// <param name="query">What to read.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The page.</returns>
    public async ValueTask<Result<ViewedPage>> Read(EventQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var cursor = Math.Max(query.After, 0);
        var kept = new List<PublicEvent>();
        while (kept.Count < query.Max)
        {
            var step = await Step(query, cursor, kept, cancellationToken).ConfigureAwait(false);
            if (step.IsError)
            {
                return step.Error;
            }

            cursor = step.Value.Cursor;
            if (!step.Value.More)
            {
                break;
            }
        }

        return new ViewedPage(kept, cursor);
    }

    private async ValueTask<Result<Progress>> Step(EventQuery query, long cursor, List<PublicEvent> kept, CancellationToken cancellationToken)
    {
        var start = cursor;
        var scan = await ledger.Scan(cursor, query.Max, query.Filter, cancellationToken).ConfigureAwait(false);
        if (scan.IsError)
        {
            return scan.Error;
        }

        foreach (var row in scan.Value.Events.Where(e => query.Keep?.Invoke(e) ?? true))
        {
            var shown = Shown(row, query.IncludeNonPublic);
            if (shown.IsError)
            {
                // the cursor stays at the last row dealt with, never past a row that couldn't be made public
                return kept.Count > 0 ? new Progress(cursor, More: false) : shown.Error;
            }

            cursor = row.Sequence;
            if (shown.Value is { } e)
            {
                kept.Add(e);
            }

            if (kept.Count == query.Max)
            {
                return new Progress(cursor, More: false);
            }
        }

        cursor = Math.Max(cursor, scan.Value.ScannedTo);
        return new Progress(cursor, More: scan.Value.Events.Count >= query.Max && cursor > start);
    }

    /// <summary>The row in its public form, or null when it has none.</summary>
    /// <param name="row">The ledger row.</param>
    /// <param name="includeNonPublic">True for a human's ledger read: types outside the catalog's public ones come too, and every payload is whole.</param>
    /// <returns>The event, or null.</returns>
    public Result<PublicEvent?> Shown(LedgerEvent row, bool includeNonPublic) => PublicEvents.For(row, json, scrubber, includeNonPublic);

    private readonly record struct Progress(long Cursor, bool More);
}
