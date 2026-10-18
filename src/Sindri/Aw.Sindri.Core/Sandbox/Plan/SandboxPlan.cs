using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>
/// The data the pure builder produces for one launch (sandbox spec §2): mounts, network rules, environment allowlist, limits, credentials.
/// Hashed (<see cref="PlanHash"/>) and summarised on 'StartedLaunch' (<see cref="ToSummary"/>).
/// </summary>
/// <param name="PlanVersion">The plan-file schema version.</param>
/// <param name="Mechanism">What confines the session.</param>
/// <param name="PlanHash">The hash of the plan (12 hex), including the resolved login source.</param>
/// <param name="Attended">True for a session the owner opened.</param>
/// <param name="Launch">The launch.</param>
/// <param name="Worktree">The cwd, or null.</param>
/// <param name="RunNamespace">The run's ref namespace.</param>
/// <param name="RunDirectory">The run directory.</param>
/// <param name="Mounts">The mount stack, bottom to top.</param>
/// <param name="Network">The network rules.</param>
/// <param name="Environment">The environment allowlist.</param>
/// <param name="Limits">The limits.</param>
/// <param name="Github">The GitHub credentials.</param>
/// <param name="Login">The resolved model login source.</param>
/// <param name="Inner">The agent CLI's own sandbox.</param>
/// <param name="Git">The git settings (command-line scope).</param>
/// <param name="Policy">The inner policy.</param>
/// <param name="Hooks">The hooks kept and omitted.</param>
public sealed record SandboxPlan(
    int PlanVersion,
    SandboxMechanism Mechanism,
    string PlanHash,
    bool Attended,
    LaunchId Launch,
    string? Worktree,
    string RunNamespace,
    string RunDirectory,
    IReadOnlyList<PlanMount> Mounts,
    PlanNetwork Network,
    IReadOnlyList<PlanEnvVar> Environment,
    PlanLimits Limits,
    GithubCredentials Github,
    ModelLoginSource Login,
    InnerSandbox Inner,
    IReadOnlyList<GitSetting> Git,
    InnerPolicy Policy,
    PlanHooks Hooks)
{
    /// <summary>The paths the session can write: every read-write bind and per-launch directory.</summary>
    public IReadOnlyList<string> Writable =>
        [.. Mounts.Where(m => m.Kind is PlanMountKind.ReadWrite or PlanMountKind.PerLaunchDir).Select(m => m.Path).Distinct(StringComparer.Ordinal)];

    /// <summary>The value-free summary the engine copies onto 'launch.started'.</summary>
    /// <returns>The summary.</returns>
    public SandboxSummary ToSummary() => new(
        Mechanism,
        PlanHash,
        Writable,
        [.. Network.Groups.Select(g => g.Name)],
        Network.Mode,
        Inner == InnerSandbox.On ? InnerState.On : InnerState.Off,   // auto is off until the self-check keeps the inner sandbox (spec §4)
        new SandboxLimitsSummary(SandboxUnits.FormatSize(Limits.MemoryMaxBytes), string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{Limits.CpuQuotaPercent}%"), Limits.TasksMax, Limits.Wall is { } w ? SandboxUnits.FormatDuration(w) : null),
        new SandboxCredentialsSummary(Github),
        Login,
        Attended);
}
