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
    IProjectorStatus projector, HealthServiceRows healthRows, ISessionSnapshot sessions, LiveSchedule? live = null)
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
        health = HealthSummary.WithProfileProblem(health, string.Equals(profile.ProblemKind, "invalid", StringComparison.Ordinal) || string.Equals(profile.ProblemKind, "unreadable", StringComparison.Ordinal), ProfileFix(loaded));   // #3436: a profile that won't load is not healthy
        var status = new GetStatusResponse(grant is not null, grant?.Holder.Pid ?? 0, grant?.Epoch ?? 0, grant?.ExpiresAt, profile, services);
        return status with { ApiVersion = SindriApi.Version, Views = views, Health = health, Sessions = SessionCounts() };
    }

    /// <summary>The collector's last pass in numbers and its counted problems (#4012: a test override is none); zeros before the first pass (collector spec CA-8).</summary>
    private SessionStatus SessionCounts()
    {
        if (sessions.Current is not { } pass)
        {
            return new SessionStatus(0, 0, 0, 0);
        }

        var counted = SessionHealth.Counted(pass.Problems);
        return new SessionStatus(pass.Sessions.Count, pass.Sessions.Count(static s => s.State == SessionState.Asking), counted.Count, pass.Sessions.Count(static s => s.State == SessionState.Running)) { ProblemRows = counted };
    }

    /// <summary>The projector's status with the ledger's head read fresh, not the projector's last-seen one: cursor below ledgerHead is the real lag.</summary>
    private async ValueTask<ViewsStatus> Views(CancellationToken cancellationToken)
    {
        var head = await ledger.Head(cancellationToken).ConfigureAwait(false);
        return head.IsSuccess ? projector.Current with { LedgerHead = head.Value } : projector.Current;
    }

    private async ValueTask<HealthReport> Health(bool running, ViewsStatus views, LoadedProfile? approved, CancellationToken cancellationToken)
    {
        var rows = await healthRows.Read(approved, cancellationToken).ConfigureAwait(false);
        var codeIndex = await healthRows.CodeIndexProblems(approved, cancellationToken).ConfigureAwait(false);
        var sources = await healthRows.SourceProblems(approved, cancellationToken).ConfigureAwait(false);   // #4010: a flagged Context index card is not "healthy"
        var agents = SessionHealth.Problems(sessions.Current?.Problems ?? []);   // #4012: a failed session source flags the Agents card the same way
        var health = HealthSummary.For(running, views, rows, healthRows.Context([.. codeIndex, .. sources, .. agents]));
        var stalled = HealthSummary.WithStalledAgents(health, await healthRows.Stalled(cancellationToken).ConfigureAwait(false));   // issue #1000: so do agent runs idle past their launch's idle time
        return HealthSummary.WithIdleAgents(stalled, await healthRows.IdleAgents(cancellationToken).ConfigureAwait(false));   // issue #1265: and sessions ended as idle that no retry replaced (ended launches, so no overlap with the live count above)
    }

    // 'on' when the APPROVED profile asks for model choice: that is what is in force, and the app never reads the word itself (model-pick spec 8).
    private static string ModelPick(LoadedProfile? approved) => approved?.Profile.Workflow?.Agent is { PickIsOn: true } ? "on" : "off";

    private ProfileStatus Profile(LoadedProfile? approved, Result<LoadedProfile> loaded, string labelPick, string surfacePick) =>
        new(loaded.IsSuccess ? loaded.Value.Hash : null, loaded.IsError ? loaded.Error.Message : null, loaded.IsSuccess ? loaded.Value.Source : null,
            ModelPick(approved), labelPick, surfacePick, ProblemKind(loaded, profiles.FileExists()));

    // 'missing' when there is no profile file yet; 'unreadable' when the file is there but the source can't read it (SND-PROFILE-004, #3345, or SND-PROFILE-001 with the file on disk); 'invalid' for any other reason it won't load (bad YAML, a broken profile rule); null when it loads (#3113, #3622): the app reads the word, not the message.
    // The health row's fix: a file the source can't read (SND-PROFILE-004 or SND-PROFILE-001) gets Setup's permissions line; any other reason is the reader's own message (#3607).
    private static string? ProfileFix(Result<LoadedProfile> loaded) =>
        loaded.IsSuccess ? null : IsCode(loaded.Error, SindriErrors.ProfileMissing) || IsCode(loaded.Error, SindriErrors.ProfileUnreadable) ? SindriErrors.ProfileUnreadableFix : loaded.Error.Message;

    private static string? ProblemKind(Result<LoadedProfile> loaded, bool fileExists) =>
        loaded.IsSuccess ? null
        : IsCode(loaded.Error, SindriErrors.ProfileUnreadable) ? "unreadable"
        : !IsCode(loaded.Error, SindriErrors.ProfileMissing) ? "invalid"
        : fileExists ? "unreadable" : "missing";

    private static bool IsCode(Error error, ErrorDefinition definition) => string.Equals(error.Code, definition.Code, StringComparison.Ordinal);

    private async ValueTask<IReadOnlyList<ServiceStatus>> Services(LoadedProfile? approved, CancellationToken cancellationToken)
    {
        var services = new List<ServiceStatus>();
        foreach (var schedule in live?.Schedules ?? healthRows.Scheduled(approved))   // what the scheduler runs, built-ins too, so the count agrees with the services list (#3623)
        {
            var last = await ledger.Latest(EventTypes.ServiceRun(schedule.Name), cancellationToken).ConfigureAwait(false);
            services.Add(new ServiceStatus(schedule.Name, schedule.Cadence.ToString(), last.IsSuccess ? last.Value?.Envelope.OccurredAt : null));
        }

        return services;
    }
}
