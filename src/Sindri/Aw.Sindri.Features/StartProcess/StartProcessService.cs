using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.StartProcess;

internal sealed class StartProcessService(ILease lease, IProcessProbe probe, HeldLease held, IApprovals approvals, ILedgerStore ledger, EventFactory events)
    : IService<StartProcessRequest, StartProcessResponse>
{
    public async ValueTask<Result<StartProcessResponse>> Handle(StartProcessRequest request, CancellationToken cancellationToken)
    {
        var self = probe.Self();
        var grant = await lease.Acquire(self, cancellationToken).ConfigureAwait(false);
        if (grant.IsError)
        {
            return grant.Error;
        }

        held.Hold(grant.Value);
        var approved = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        var hash = approved.IsSuccess ? approved.Value?.Hash : null;
        var entry = events.Entry(EventTypes.ProcessStarted, new ProcessStarted(self.Host, self.Pid, grant.Value.Epoch, hash),
            string.Create(CultureInfo.InvariantCulture, $"{EventTypes.ProcessStarted}:{grant.Value.Epoch}"));
        var recorded = await ledger.Append(entry, grant.Value, cancellationToken).ConfigureAwait(false);
        if (recorded.IsError)
        {
            held.Clear();   // the start isn't recorded, so don't keep the lease: give it back for the next start
            _ = await lease.Release(grant.Value, cancellationToken).ConfigureAwait(false);
            return recorded.Error;
        }

        return new StartProcessResponse(grant.Value, hash);
    }
}
