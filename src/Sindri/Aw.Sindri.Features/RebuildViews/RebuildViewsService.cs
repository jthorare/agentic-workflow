using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.RebuildViews;

/// <summary>
/// Records 'views.rebuilt' (the projector's cursor before, and the ledger head the replay will cover; plan G4), then
/// asks the projector to rebuild. It never waits for the views, so it runs while they are stalled or already
/// rebuilding (G17). Without the lease it records nothing: SND-LEASE-002.
/// </summary>
internal sealed class RebuildViewsService(IProjectorStatus status, IProjectorControl control, ILedgerStore ledger, HeldLease held, EventFactory events,
    ICallerContextAccessor callers, IIdGenerator ids) : IService<RebuildViewsRequest, CommandAccepted>
{
    public async ValueTask<Result<CommandAccepted>> Handle(RebuildViewsRequest request, CancellationToken cancellationToken)
    {
        if (held.Current is not { } grant)
        {
            return SindriErrors.LeaseLost.With("this process doesn't hold the lease, so the rebuild wasn't recorded (fix: restart sindri)");
        }

        var head = await ledger.Head(cancellationToken).ConfigureAwait(false);
        if (head.IsError)
        {
            return head.Error;
        }

        var commandId = callers.Current?.CommandId ?? ids.New<CommandId>();
        var entry = events.Entry(EventTypes.ViewsRebuilt, new ViewsRebuilt(commandId, status.Current.Cursor, head.Value),
            $"{EventTypes.ViewsRebuilt}:{commandId.Value}");
        var appended = await ledger.Append(entry, grant, cancellationToken).ConfigureAwait(false);
        if (appended.IsError)
        {
            return appended.Error;
        }

        control.RequestRebuild();
        return new CommandAccepted(commandId, appended.Value.Sequence);
    }
}
