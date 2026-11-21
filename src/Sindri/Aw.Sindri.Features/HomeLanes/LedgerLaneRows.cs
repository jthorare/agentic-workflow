using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Lanes;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.HomeLanes;

/// <summary>
/// <see cref="ILaneRows"/> over the ledger and the needs_you view: Open lists the view's rows of the three lane kinds, Raise and Clear append
/// 'home.lane-row.raised' and 'home.lane-row.cleared', which the NeedsYouProjection turns into the row. Appends are skipped when this process holds no lease (the next tick tries again).
/// </summary>
/// <param name="held">The lease this process holds.</param>
/// <param name="ledger">Appends the events.</param>
/// <param name="events">Builds the entries.</param>
/// <param name="views">Reads the needs_you view.</param>
/// <param name="clock">The time.</param>
internal sealed class LedgerLaneRows(HeldLease held, ILedgerStore ledger, EventFactory events, IViewReader views, TimeProvider clock) : ILaneRows
{
    public async ValueTask<Result<IReadOnlyList<NeedsYouRowId>>> Open(CancellationToken cancellationToken)
    {
        var listed = await views.List<NeedsYouRowRecord>(ViewNames.NeedsYou, cancellationToken).ConfigureAwait(false);
        return listed.IsError
            ? listed.Error
            : Results.Success<IReadOnlyList<NeedsYouRowId>>([.. listed.Value.Where(static r => r.Row.Kind is NeedsYouKind.LaneRecycle or NeedsYouKind.AgentLimited or NeedsYouKind.PrReadyMerge).Select(static r => r.Row.Id)]);
    }

    public async ValueTask<Result<Unit>> Raise(NeedsYouKind kind, string key, string title, string reason, CancellationToken cancellationToken)
    {
        var at = clock.GetUtcNow();
        var wire = LaneTriggers.Wire(kind);
        return await Append(EventTypes.HomeLaneRowRaised, new HomeLaneRowRaised(wire, key, title, reason, at), $"{EventTypes.HomeLaneRowRaised}:{wire}:{key}:{at.UtcTicks.ToString(CultureInfo.InvariantCulture)}", cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<Result<Unit>> Clear(NeedsYouRowId id, CancellationToken cancellationToken)
    {
        var at = clock.GetUtcNow();
        var split = id.Value.IndexOf(':', StringComparison.Ordinal);
        var kind = id.Value[..split];
        var key = id.Value[(split + 1)..];
        return await Append(EventTypes.HomeLaneRowCleared, new HomeLaneRowCleared(kind, key, at), $"{EventTypes.HomeLaneRowCleared}:{kind}:{key}:{at.UtcTicks.ToString(CultureInfo.InvariantCulture)}", cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<Unit>> Append<TPayload>(string type, TPayload payload, string idempotencyKey, CancellationToken cancellationToken)
        where TPayload : notnull
    {
        if (held.Current is not { } lease)
        {
            return Unit.Value;
        }

        var appended = await ledger.Append(events.Entry(type, payload, idempotencyKey), lease, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : Unit.Value;
    }
}
