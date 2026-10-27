using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Core.Views;

/// <summary>Folds 'service.ran', 'service.paused' and 'service.resumed' into the 'services' view (API spec §2.3): one row per service, keyed by its name, holding its last run, whether it is paused and the slot of its last scheduled run (a manual run moves the last run but not the slot).</summary>
/// <param name="events">Reads the payloads through the package's AwJson options.</param>
internal sealed class ServicesProjection(EventFactory events) : IProjection
{
    public IReadOnlyList<string> Views => [ViewNames.Services];

    public bool Handles(string eventType) =>
        string.Equals(eventType, EventTypes.ServiceRan, StringComparison.Ordinal)
        || string.Equals(eventType, EventTypes.ServicePaused, StringComparison.Ordinal)
        || string.Equals(eventType, EventTypes.ServiceResumed, StringComparison.Ordinal);

    public async ValueTask<Result<Unit>> Apply(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recorded);
        ArgumentNullException.ThrowIfNull(rows);
        return recorded.Envelope.Type switch
        {
            EventTypes.ServicePaused => await Change<ServicePaused>(recorded, rows, static p => p.Service, Fold, cancellationToken).ConfigureAwait(false),
            EventTypes.ServiceResumed => await Change<ServiceResumed>(recorded, rows, static r => r.Service, Fold, cancellationToken).ConfigureAwait(false),
            _ => await Change<ServiceRan>(recorded, rows, static r => r.Service, Fold, cancellationToken).ConfigureAwait(false),
        };
    }

    private async ValueTask<Result<Unit>> Change<TPayload>(LedgerEvent recorded, IViewRows rows, Func<TPayload, ServiceName> service,
        Func<ServiceRow?, TPayload, LedgerEvent, ServiceRow> fold, CancellationToken cancellationToken)
    {
        var payload = events.Payload<TPayload>(recorded);
        if (payload.IsError)
        {
            return payload.Error;   // an unreadable payload stalls the projector here (SND-VIEW-001)
        }

        var key = service(payload.Value).Value;
        var row = await rows.Get<ServiceRow>(ViewNames.Services, key, cancellationToken).ConfigureAwait(false);
        return row.IsError ? row.Error : await rows.Put(ViewNames.Services, key, fold(row.Value, payload.Value, recorded), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The row after <paramref name="ran"/>: the run it records becomes the last run.</summary>
    /// <param name="row">The row so far, or null before the service's first run.</param>
    /// <param name="ran">The payload.</param>
    /// <param name="recorded">The recorded event (its time and sequence).</param>
    /// <returns>The new row.</returns>
    public static ServiceRow Fold(ServiceRow? row, ServiceRan ran, LedgerEvent recorded)
    {
        ArgumentNullException.ThrowIfNull(ran);
        ArgumentNullException.ThrowIfNull(recorded);
        var last = new LastRun(ran.Slot, ran.Outcome, ran.Code, recorded.Envelope.OccurredAt, ran.Detail);
        return new ServiceRow(ran.Service, last, row?.Paused ?? false, recorded.Sequence, ran.Job is null ? ran.Slot : row?.ScheduledSlot, row?.PausedBy, row?.PausedAt);
    }

    /// <summary>The row after a 'service.paused': paused, the last run and scheduled slot kept (a row is made when there is none).</summary>
    /// <param name="row">The row so far, or null.</param>
    /// <param name="paused">The payload.</param>
    /// <param name="recorded">The recorded event.</param>
    /// <returns>The new row.</returns>
    public static ServiceRow Fold(ServiceRow? row, ServicePaused paused, LedgerEvent recorded)
    {
        ArgumentNullException.ThrowIfNull(paused);
        ArgumentNullException.ThrowIfNull(recorded);
        return new ServiceRow(paused.Service, row?.LastRun, Paused: true, recorded.Sequence, row?.ScheduledSlot, recorded.Envelope.Actor, recorded.Envelope.OccurredAt);
    }

    /// <summary>The row after a 'service.resumed': not paused, the rest kept.</summary>
    /// <param name="row">The row so far, or null.</param>
    /// <param name="resumed">The payload.</param>
    /// <param name="recorded">The recorded event.</param>
    /// <returns>The new row.</returns>
    public static ServiceRow Fold(ServiceRow? row, ServiceResumed resumed, LedgerEvent recorded)
    {
        ArgumentNullException.ThrowIfNull(resumed);
        ArgumentNullException.ThrowIfNull(recorded);
        return new ServiceRow(resumed.Service, row?.LastRun, Paused: false, recorded.Sequence, row?.ScheduledSlot);
    }
}
