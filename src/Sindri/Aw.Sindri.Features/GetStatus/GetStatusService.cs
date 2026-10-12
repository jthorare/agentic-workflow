using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Features.GetStatus;

internal sealed class GetStatusService(HeldLease held, IApprovals approvals, IProfileSource live, ILedgerStore ledger, EventFactory events)
    : IService<GetStatusRequest, GetStatusResponse>
{
    public async ValueTask<Result<GetStatusResponse>> Handle(GetStatusRequest request, CancellationToken cancellationToken)
    {
        var approved = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        var loaded = await live.Load(cancellationToken).ConfigureAwait(false);
        var approvedProfile = approved.IsSuccess ? approved.Value : null;
        var services = await Services(approvedProfile, cancellationToken).ConfigureAwait(false);
        var recorded = RecordedApproval.From(await ledger.Latest(EventTypes.ProfileApproved, cancellationToken).ConfigureAwait(false), events);
        if (approved.IsError || recorded.IsError)
        {
            // like validate, the approve preview and doctor: an approval or a ledger that can't be read isn't "needs approval"
            return approved.IsError ? approved.Error : recorded.Error;
        }

        var grant = held.Current;
        return new GetStatusResponse(grant is not null, grant?.Holder.Pid ?? 0, grant?.Epoch ?? 0, grant?.ExpiresAt,
            Profile(approvedProfile, recorded.Value, loaded), services);
    }

    private static ProfileStatus Profile(LoadedProfile? approved, RecordedApproval? recorded, Result<LoadedProfile> loaded)
    {
        var approvedHash = approved?.Hash;
        var liveHash = loaded.IsSuccess ? loaded.Value.Hash : (Core.Ids.ProfileHash?)null;
        var changed = approvedHash is not null && liveHash is not null && liveHash != approvedHash;
        return new ProfileStatus(approvedHash, liveHash, changed, loaded.IsError ? loaded.Error.Message : null, loaded.IsSuccess ? loaded.Value.Source : null,
            ProfileRules.NeedsApproval(approvedHash, recorded?.Hash, liveHash));
    }

    private async ValueTask<IReadOnlyList<ServiceStatus>> Services(LoadedProfile? approved, CancellationToken cancellationToken)
    {
        var services = new List<ServiceStatus>();
        foreach (var schedule in approved is null ? [] : ServiceSchedule.From(approved.Profile, DateTimeOffset.MinValue))
        {
            var last = await ledger.Latest(EventTypes.ServiceRun(schedule.Name), cancellationToken).ConfigureAwait(false);
            services.Add(new ServiceStatus(schedule.Name, schedule.Cadence.ToString(), last.IsSuccess ? last.Value?.Envelope.OccurredAt : null));
        }

        return services;
    }
}
