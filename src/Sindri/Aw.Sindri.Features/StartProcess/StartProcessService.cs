using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.StartProcess;

/// <summary>
/// Takes the lease, then brings up the Temporal dev server (adopting one a dead serve left running), then records the
/// start. The server comes after the lease, so a second serve refused by the lease never touches it; a failure after the
/// lease gives the lease back.
/// </summary>
internal sealed class StartProcessService(
    ILease lease, IProcessProbe probe, HeldLease held, IApprovals approvals, ILedgerStore ledger, EventFactory events, ITemporalServer temporal)
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
        var server = await temporal.Ensure(cancellationToken).ConfigureAwait(false);
        if (server.IsError)
        {
            await GiveBack(grant.Value, cancellationToken).ConfigureAwait(false);
            return server.Error;
        }

        var approved = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        var hash = approved.IsSuccess ? approved.Value?.Hash : null;
        var entry = events.Entry(EventTypes.ProcessStarted, new ProcessStarted(self.Host, self.Pid, grant.Value.Epoch, hash),
            string.Create(CultureInfo.InvariantCulture, $"{EventTypes.ProcessStarted}:{grant.Value.Epoch}"));
        var recorded = await ledger.Append(entry, grant.Value, cancellationToken).ConfigureAwait(false);
        if (recorded.IsError)
        {
            _ = await temporal.Stop(cancellationToken).ConfigureAwait(false);
            await GiveBack(grant.Value, cancellationToken).ConfigureAwait(false);   // the start isn't recorded: give the lease back for the next start
            return recorded.Error;
        }

        return new StartProcessResponse(grant.Value, hash, server.Value);
    }

    private async ValueTask GiveBack(LeaseGrant grant, CancellationToken cancellationToken)
    {
        held.Clear();
        _ = await lease.Release(grant, cancellationToken).ConfigureAwait(false);
    }
}
