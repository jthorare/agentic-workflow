using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Health;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.GetStatus;

internal sealed class GetStatusService(HeldLease held, ProfileReads profiles, ILedgerReader ledger, EventFactory events,
    IProjectorStatus projector, HealthServiceRows healthRows, ISessionSnapshot sessions)
    : IService<GetStatusRequest, GetStatusResponse>
{
    public async ValueTask<Result<GetStatusResponse>> Handle(GetStatusRequest request, CancellationToken cancellationToken)
    {
        var approved = await profiles.Approved(cancellationToken).ConfigureAwait(false);
        var loaded = await profiles.Live(cancellationToken).ConfigureAwait(false);
        var approvedProfile = approved.IsSuccess ? approved.Value : null;
        var services = await Services(approvedProfile, cancellationToken).ConfigureAwait(false);
        var recorded = RecordedApproval.From(await ledger.Latest(EventTypes.ProfileApproved, cancellationToken).ConfigureAwait(false), events);
        if (approved.IsError || recorded.IsError)
        {
            // like validate, the approve preview and doctor: an approval or a ledger that can't be read isn't "needs approval"
            return approved.IsError ? approved.Error : recorded.Error;
        }

        var grant = held.Current;
        var profile = Profile(approvedProfile, recorded.Value, loaded);
        var views = await Views(cancellationToken).ConfigureAwait(false);
        var health = await Health(grant is not null, views, profile, approvedProfile, cancellationToken).ConfigureAwait(false);
        var status = new GetStatusResponse(grant is not null, grant?.Holder.Pid ?? 0, grant?.Epoch ?? 0, grant?.ExpiresAt, profile, services);
        return status with { ApiVersion = SindriApi.Version, Views = views, Health = health, Sessions = SessionCounts() };
    }

    /// <summary>The collector's last pass in three numbers; zeros before the first pass (collector spec CA-8).</summary>
    private SessionStatus SessionCounts() =>
        sessions.Current is { } pass ? new SessionStatus(pass.Sessions.Count, pass.Sessions.Count(static s => s.State == SessionState.Asking), pass.Problems.Count, pass.Sessions.Count(static s => s.State == SessionState.Running)) : new SessionStatus(0, 0, 0, 0);

    /// <summary>The projector's status with the ledger's head read fresh, not the projector's last-seen one: cursor below ledgerHead is the real lag.</summary>
    private async ValueTask<ViewsStatus> Views(CancellationToken cancellationToken)
    {
        var head = await ledger.Head(cancellationToken).ConfigureAwait(false);
        return head.IsSuccess ? projector.Current with { LedgerHead = head.Value } : projector.Current;
    }

    private async ValueTask<HealthReport> Health(bool running, ViewsStatus views, ProfileStatus profile, LoadedProfile? approved, CancellationToken cancellationToken)
    {
        var rows = await healthRows.Read(approved, cancellationToken).ConfigureAwait(false);
        var problems = await healthRows.CodeIndexProblems(approved, cancellationToken).ConfigureAwait(false);
        var standing = profile.ChangedSinceApproval ? ProfileApproval.Changed : profile.NeedsApproval ? ProfileApproval.NeedsApproval : ProfileApproval.Approved;
        var health = HealthSummary.For(running, views, rows, standing, problems);
        var stuck = HealthSummary.WithStuck(health, await healthRows.Stuck(cancellationToken).ConfigureAwait(false));   // API P13: stuck workflow instances make it degraded
        var parked = HealthSummary.WithUnavailable(stuck, await healthRows.Unavailable(cancellationToken).ConfigureAwait(false));   // a Step whose dependency is down parks asking no one: health shows it
        return HealthSummary.WithStalledAgents(parked, await healthRows.Stalled(cancellationToken).ConfigureAwait(false));   // issue #1000: so do agent runs idle past their launch's idle time
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
