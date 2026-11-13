using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
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

internal sealed class GetStatusService(HeldLease held, ProfileReads profiles, ILedgerReader ledger,
    IProjectorStatus projector, HealthServiceRows healthRows, ISessionSnapshot sessions)
    : IService<GetStatusRequest, GetStatusResponse>
{
    public async ValueTask<Result<GetStatusResponse>> Handle(GetStatusRequest request, CancellationToken cancellationToken)
    {
        var approved = await profiles.Approved(cancellationToken).ConfigureAwait(false);
        var loaded = await profiles.Live(cancellationToken).ConfigureAwait(false);
        var approvedProfile = approved.IsSuccess ? approved.Value : null;
        var services = await Services(approvedProfile, cancellationToken).ConfigureAwait(false);
        var grant = held.Current;
        var profile = Profile(approvedProfile, loaded, await profiles.LabelPick(approvedProfile, cancellationToken).ConfigureAwait(false), await profiles.SurfacePick(approvedProfile, cancellationToken).ConfigureAwait(false));
        var views = await Views(cancellationToken).ConfigureAwait(false);
        var health = await Health(grant is not null, views, approvedProfile, cancellationToken).ConfigureAwait(false);
        health = healthRows.WithStaleBuild(health);
        var status = new GetStatusResponse(grant is not null, grant?.Holder.Pid ?? 0, grant?.Epoch ?? 0, grant?.ExpiresAt, profile, services);
        return status with { ApiVersion = SindriApi.Version, Views = views, Health = health, Sessions = SessionCounts(), Build = healthRows.Build() };
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

    private async ValueTask<HealthReport> Health(bool running, ViewsStatus views, LoadedProfile? approved, CancellationToken cancellationToken)
    {
        var rows = await healthRows.Read(approved, cancellationToken).ConfigureAwait(false);
        var problems = await healthRows.CodeIndexProblems(approved, cancellationToken).ConfigureAwait(false);
        var health = HealthSummary.For(running, views, rows, healthRows.Context(problems));
        var stalled = HealthSummary.WithStalledAgents(health, await healthRows.Stalled(cancellationToken).ConfigureAwait(false));   // issue #1000: so do agent runs idle past their launch's idle time
        return HealthSummary.WithIdleAgents(stalled, await healthRows.IdleAgents(cancellationToken).ConfigureAwait(false));   // issue #1265: and sessions ended as idle that no retry replaced (ended launches, so no overlap with the live count above)
    }

    // 'on' when the APPROVED profile asks for model choice: that is what is in force, and the app never reads the word itself (model-pick spec 8).
    private static string ModelPick(LoadedProfile? approved) => approved?.Profile.Workflow?.Agent is { PickIsOn: true } ? "on" : "off";

    private ProfileStatus Profile(LoadedProfile? approved, Result<LoadedProfile> loaded, string labelPick, string surfacePick) =>
        new(loaded.IsSuccess ? loaded.Value.Hash : null, loaded.IsError ? loaded.Error.Message : null, loaded.IsSuccess ? loaded.Value.Source : null,
            ModelPick(approved), labelPick, surfacePick, ProblemKind(loaded, profiles.FileExists()));

    // 'missing' when there is no profile file yet, 'invalid' for any other reason it won't load (a file the source can't read answers SND-PROFILE-001 too, so the file must be absent), null when it loads (#3113): the app reads the word, not the message.
    private static string? ProblemKind(Result<LoadedProfile> loaded, bool fileExists) =>
        loaded.IsSuccess ? null : string.Equals(loaded.Error.Code, SindriErrors.ProfileMissing.Code, StringComparison.Ordinal) && !fileExists ? "missing" : "invalid";

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
