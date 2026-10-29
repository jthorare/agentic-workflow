using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;

namespace Aw.Sindri.Features.GetSandboxStatus;

/// <summary>Says what the last sandbox check found and how the sandbox is set up (sandbox spec §8). Read-only and cheap: it runs no check and no probe.</summary>
/// <param name="last">The newest check.</param>
/// <param name="approved">The approved profile's sandbox section.</param>
/// <param name="machine">The memory and cores the limits are read against.</param>
/// <param name="learn">The ledger's learn history.</param>
/// <param name="platform">Names the mechanism the platform runs.</param>
/// <param name="clock">Reads the time the learn windows are measured at.</param>
internal sealed class GetSandboxStatusService(LastSandboxCheck last, IApprovedProfile approved, ISandboxMachine machine, ILearnHistory learn, ISandboxPlatformName platform, TimeProvider clock)
    : IService<GetSandboxStatusRequest, SandboxStatusResponse>
{
    private const string NoMechanism = "none";
    private const string Unavailable = "SND-LAUNCH-014";
    private static readonly string[] _builtInGroups = ["model", "packages", "github"];

    public async ValueTask<Result<SandboxStatusResponse>> Handle(GetSandboxStatusRequest request, CancellationToken cancellationToken)
    {
        var loaded = await approved.Latest(cancellationToken).ConfigureAwait(false);
        var profile = loaded.IsSuccess ? loaded.Value?.Profile : null;
        var settings = profile?.Sandbox;
        var (checkedAt, ready, blockers, mechanism) = Checked(last.Latest);
        return new SandboxStatusResponse(
            checkedAt, ready, blockers, mechanism,
            Groups(settings),
            Limits(settings),
            settings?.Login?.Source ?? SandboxDefaults.Login,
            await LearnLines(profile, settings, cancellationToken).ConfigureAwait(false));
    }

    private (DateTimeOffset? At, bool? Ready, SandboxBlockerLine[] Blockers, string Mechanism) Checked(SandboxCheckRecord? newest) =>
        (newest?.At, newest?.Report.Ready, [.. newest?.Report.Blockers.Select(SandboxBlockerLine.From) ?? []], Mechanism(newest?.Report));

    /// <summary>The mechanism this box runs, from the newest check: a failed probe (-014) means none, otherwise the platform's. Never probes.</summary>
    private string Mechanism(SandboxCheckReport? report) => report is not null && report.Blockers.Any(static b => string.Equals(b.Code.Error.Code, Unavailable, StringComparison.Ordinal))
        ? NoMechanism
        : platform.Mechanism;

    /// <summary>The built-in group names (model, packages, github), then the profile's own.</summary>
    private static string[] Groups(SandboxSettings? settings) =>
        [.. _builtInGroups.Concat(settings?.Network?.Groups?.Keys ?? []).Distinct(StringComparer.Ordinal)];

    private SandboxLimitsSummary Limits(SandboxSettings? settings)
    {
        var limits = LimitsResolver.Resolve(new LimitsInput(settings?.Limits, AttendedSettings: null, Attended: false, StepTimeout: null, LongMax: null, machine.RamBytes, machine.Cores));
        return new SandboxLimitsSummary(SandboxUnits.FormatSize(limits.MemoryMaxBytes), string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{limits.CpuQuotaPercent}%"), limits.TasksMax,
            limits.Wall is { } wall ? SandboxUnits.FormatDuration(wall) : null);
    }

    /// <summary>The repos whose learn window is open now: learn mode is on, the repo has started at least one learn run, and neither ten runs, seven days nor an early end closed it.</summary>
    private async ValueTask<SandboxLearnLine[]> LearnLines(SindriProfile? profile, SandboxSettings? settings, CancellationToken cancellationToken)
    {
        var lines = new List<SandboxLearnLine>();
        foreach (var repo in profile?.Repos ?? [])
        {
            var history = await learn.Read(repo.Name, cancellationToken).ConfigureAwait(false);
            if (history.LearnRuns > 0 && LearnWindow.Decide(settings?.Network?.Mode, history, clock.GetUtcNow()) is { Mode: NetworkMode.Learn, LearnUntil: { } until })
            {
                lines.Add(new SandboxLearnLine(repo.Name, until));
            }
        }

        return [.. lines];
    }
}
