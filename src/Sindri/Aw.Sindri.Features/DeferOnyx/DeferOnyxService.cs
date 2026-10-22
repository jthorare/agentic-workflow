using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Features.DeferOnyx;

/// <summary>
/// Records the owner's "Not now" for Onyx, or the change of mind (setup-once spec §5.6; desk): one 'onyx.deferred' keyed by the command, so the latest one is the choice in force.
/// The Onyx step reads it back from the ledger; nothing else changes.
/// </summary>
/// <param name="ledger">Appends the event.</param>
/// <param name="held">The lease this process holds.</param>
/// <param name="events">Builds the entry.</param>
/// <param name="callers">The caller: the command id.</param>
internal sealed class DeferOnyxService(ILedgerStore ledger, HeldLease held, EventFactory events, ICallerContextAccessor callers) : IService<DeferOnyxRequest, DeferOnyxResponse>
{
    public async ValueTask<Result<DeferOnyxResponse>> Handle(DeferOnyxRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current is not { } caller)
        {
            return SindriErrors.AuthRefused.With("DeferOnyx needs a caller");
        }

        if (held.Current is not { } grant)
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)");
        }

        var entry = events.Entry(EventTypes.OnyxDeferred, new OnyxDeferred(caller.CommandId, request.Deferred), $"{EventTypes.OnyxDeferred}:{caller.CommandId.Value}");
        var appended = await ledger.Append(entry, grant, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : new DeferOnyxResponse(request.Deferred, appended.Value.Sequence);
    }
}
