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
        var capped = await Rows(EventTypes.CatchUpCapped, cancellationToken).ConfigureAwait(false);
        var polled = await Rows(EventTypes.ConnectionPolled, cancellationToken).ConfigureAwait(false);
        if (capped.IsError || polled.IsError)
        {
            var error = capped.IsError ? capped.Error : polled.Error;
            return [new DoctorCheck(Name, CheckState.Warn, $"{error.Code}: {error.Message}", Fix: null)];
        }

        var open = Open(capped.Value, LastPolls(polled.Value));
        if (open.Count == 0)
        {
            return [new DoctorCheck(Name, CheckState.Ok, "no capped or gapped read", Fix: null)];
        }

        var detail = string.Join("; ", open.Values.OrderBy(static c => c.Connection, StringComparer.Ordinal).ThenBy(static c => c.Kind, StringComparer.Ordinal)
            .Select(static c => string.Create(CultureInfo.InvariantCulture, $"{c.Connection} {c.Kind}: {c.Reason} ({c.At.UtcDateTime:yyyy-MM-dd HH:mm}Z)")));
        return [new DoctorCheck(Name, CheckState.Warn, "capped read: " + detail, Fix)];
    }

    private Dictionary<(string, string), long> LastPolls(List<LedgerEvent> polled)
    {
        var lastPoll = new Dictionary<(string, string), long>();
        foreach (var row in polled)
        {
            if (events.Payload<ConnectionPolled>(row) is { IsSuccess: true, Value: var p })
            {
                lastPoll[(p.Connection, p.Kind)] = row.Sequence;
            }
        }

        return lastPoll;
    }

    /// <summary>The newest capped read of each pair that no later poll of the pair followed.</summary>
    private Dictionary<(string, string), CatchUpCapped> Open(List<LedgerEvent> capped, Dictionary<(string, string), long> lastPoll)
    {
        var open = new Dictionary<(string, string), CatchUpCapped>();
        foreach (var row in capped)
        {
            if (events.Payload<CatchUpCapped>(row) is not { IsSuccess: true, Value: var c })
            {
                continue;
            }

            var key = (c.Connection, c.Kind);
            if (lastPoll.TryGetValue(key, out var polledAt) && polledAt > row.Sequence)
            {
                _ = open.Remove(key);
            }
            else
            {
                open[key] = c;
            }
        }

        return open;
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
