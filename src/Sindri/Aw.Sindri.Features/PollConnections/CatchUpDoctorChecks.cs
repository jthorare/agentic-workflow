using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Features.PollConnections;

/// <summary>
/// Doctor's 'connections:catchup' row (#2437): the last capped or gapped read of each (connection, event kind), with its reason and time. It reads what the poll recorded: 'catchup.capped' rows,
/// and a later 'connection.polled' row for the same pair clears the pair (the poll after the cap reached what it had skipped or stopped being capped). No new storage.
/// </summary>
/// <param name="ledger">The ledger, read-only.</param>
/// <param name="events">Reads a payload.</param>
internal sealed class CatchUpDoctorChecks(ILedgerReader ledger, EventFactory events) : IDoctorChecks
{
    internal const string Name = "connections:catchup";

    private const int Page = 500;
    private const string Fix = "events older than the saved cursor may have been skipped: read the history from the provider, or check that a later poll returns events for the pair";

    public async ValueTask<IReadOnlyList<DoctorCheck>> Run(CancellationToken cancellationToken)
    {
        var capped = await Capped(cancellationToken).ConfigureAwait(false);
        if (capped.IsError)
        {
            return [new DoctorCheck(Name, CheckState.Warn, $"{capped.Error.Code}: {capped.Error.Message}", Fix: null)];
        }

        // why: only the (rare) capped pairs are looked at; each gets one index seek for its newest poll (the key ends in fixed-width UtcTicks, so the greatest key is the newest), so the cost never grows with the polls in the ledger (#2480).
        // A ':' inside a connection name can make a longer-named neighbour's polls match the prefix: that gives a false warn at worst, never a missed one.
        var open = new List<CatchUpCapped>();
        foreach (var (cap, sequence) in capped.Value.Values.OrderBy(static c => c.Cap.Connection, StringComparer.Ordinal).ThenBy(static c => c.Cap.Kind, StringComparer.Ordinal))
        {
            var latest = await ledger.LatestByKeyPrefix($"{EventTypes.ConnectionPolled}:{cap.Connection}:{cap.Kind}:", cancellationToken).ConfigureAwait(false);
            if (latest.IsError)
            {
                return [new DoctorCheck(Name, CheckState.Warn, $"{latest.Error.Code}: {latest.Error.Message}", Fix: null)];
            }

            if (!Cleared(latest.Value, cap, sequence))
            {
                open.Add(cap);
            }
        }

        if (open.Count == 0)
        {
            return [new DoctorCheck(Name, CheckState.Ok, "no capped or gapped read", Fix: null)];
        }

        var detail = string.Join("; ", open
            .Select(static c => string.Create(CultureInfo.InvariantCulture, $"{c.Connection} {c.Kind}: {c.Reason} ({c.At.UtcDateTime:yyyy-MM-dd HH:mm}Z)")));
        return [new DoctorCheck(Name, CheckState.Warn, "capped read: " + detail, Fix)];
    }

    /// <summary>True when the pair's newest poll came after its newest cap (the key prefix is checked against the payload, since a name or kind holding ':' could blur it).</summary>
    private bool Cleared(LedgerEvent? latestPoll, CatchUpCapped cap, long capSequence) =>
        latestPoll is { } row && row.Sequence > capSequence
            && events.Payload<ConnectionPolled>(row) is { IsSuccess: true, Value: var p }
            && string.Equals(p.Connection, cap.Connection, StringComparison.Ordinal) && string.Equals(p.Kind, cap.Kind, StringComparison.Ordinal);

    /// <summary>The newest capped read of each (connection, kind) with its sequence.</summary>
    private async ValueTask<Result<Dictionary<(string, string), (CatchUpCapped Cap, long Sequence)>>> Capped(CancellationToken cancellationToken)
    {
        var rows = await Rows(EventTypes.CatchUpCapped, cancellationToken).ConfigureAwait(false);
        if (rows.IsError)
        {
            return rows.Error;
        }

        var newest = new Dictionary<(string, string), (CatchUpCapped, long)>();
        foreach (var row in rows.Value)
        {
            if (events.Payload<CatchUpCapped>(row) is { IsSuccess: true, Value: var c })
            {
                newest[(c.Connection, c.Kind)] = (c, row.Sequence);
            }
        }

        return newest;
    }

    private async ValueTask<Result<List<LedgerEvent>>> Rows(string type, CancellationToken cancellationToken)
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
