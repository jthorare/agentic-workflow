using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Features.StopProcess;

internal sealed class StopProcessService(HeldLease held, ILease lease, ILedgerStore ledger, EventFactory events, IShutdown shutdown)
    : IService<StopProcessRequest, StopProcessResponse>
{
    public async ValueTask<Result<StopProcessResponse>> Handle(StopProcessRequest request, CancellationToken cancellationToken)
    {
        var grant = held.Current;
        if (grant is null)
        {
            shutdown.Request();
            return new StopProcessResponse(0, 0);
        }

        var entry = events.Entry(EventTypes.ProcessStopped, new ProcessStopped(grant.Holder.Pid, grant.Epoch, request.Reason),
            string.Create(CultureInfo.InvariantCulture, $"{EventTypes.ProcessStopped}:{grant.Epoch}"));
        var recorded = await ledger.Append(entry, grant, cancellationToken).ConfigureAwait(false);
        held.Clear();         // first, so a renewal finishing now can't hold the grant again
        shutdown.Request();   // before the release, so a renewal that runs after it sees a stop, not a lost lease
        var released = await lease.Release(grant, cancellationToken).ConfigureAwait(false);
        return recorded.IsError ? recorded.Error
            : released.IsError ? released.Error
            : new StopProcessResponse(grant.Holder.Pid, grant.Epoch);
    }
}
