using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Features.Sessions;

/// <summary>Reads the last 24 hours of 'fleet.changed' rows back, so a restart ledgers 'appeared' only for new sessions and 'gone' for ones that ended while it was down.</summary>
internal sealed class RestoreFleetHistoryService(ILedgerReader reader, EventFactory events, CollectorMemory memory, TimeProvider clock)
    : IService<RestoreFleetHistoryRequest, RestoreFleetHistoryResponse>
{
    private const int Page = 500;
    private static readonly TimeSpan _window = TimeSpan.FromHours(24);
    private static readonly LedgerFilter _filter = new(TypeFilter.Parse(EventTypes.FleetChanged).Value, Item: null);

    public async ValueTask<Result<RestoreFleetHistoryResponse>> Handle(RestoreFleetHistoryRequest request, CancellationToken cancellationToken)
    {
        var head = await reader.Head(cancellationToken).ConfigureAwait(false);
        if (head.IsError)
        {
            return head.Error;
        }

        var since = clock.GetUtcNow() - _window;
        var rows = new List<FleetHistoryRow>();
        for (var cursor = 0L; cursor < head.Value;)
        {
            var scan = await reader.Scan(cursor, Page, _filter, cancellationToken).ConfigureAwait(false);
            if (scan.IsError)
            {
                return scan.Error;
            }

            rows.AddRange(scan.Value.Events.Where(e => e.Envelope.OccurredAt >= since).Select(Row).OfType<FleetHistoryRow>());
            if (scan.Value.ScannedTo <= cursor)
            {
                break;
            }

            cursor = scan.Value.ScannedTo;
        }

        memory.Tracker = FleetChangeRules.Restore(rows);
        memory.Restored = true;
        return new RestoreFleetHistoryResponse(memory.Tracker.Reported.Count);
    }

    private FleetHistoryRow? Row(LedgerEvent recorded)
    {
        var payload = events.Payload<FleetChanged>(recorded);
        return payload.IsError ? null : new FleetHistoryRow(new SessionRef(payload.Value.Provider, payload.Value.SessionId), payload.Value.Change, recorded.Envelope.OccurredAt) { Repo = payload.Value.Repo, HostKind = payload.Value.Host, LaunchId = payload.Value.LaunchId };
    }
}
