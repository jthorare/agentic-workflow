using Aw.Kernel;
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

    /// <summary>
    /// The plan 'sindri sandbox exec' runs (sandbox spec §3.6): this launch's plan with the worktree, the run's ref directories and the run directory read-only (the live agent may still be using them),
    /// a fresh run area (so a fresh empty scratch, spool and login directory), no session home, no launcher-supplied variable (the launch secret, a model key) and no GitHub token, under its own launch id and socket directory.
    /// Pure: the caller makes the directories and starts the egress.
    /// </summary>
    /// <param name="execLaunch">The exec's own launch id (its scope and its denial counter).</param>
    /// <param name="execArea">The exec's own run area, replacing this plan's.</param>
    /// <param name="execNetDir">The exec's own short socket directory.</param>
    /// <returns>The plan, or SND-CLI-003 when this plan names no run area (no AW_SANDBOX_PLAN variable).</returns>
    public Result<SandboxPlan> ForExec(LaunchId execLaunch, string execArea, string execNetDir)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(execArea);
        ArgumentException.ThrowIfNullOrWhiteSpace(execNetDir);
        if (EnvValue("AW_SANDBOX_PLAN") is not { } file || file.LastIndexOf('/') is not (> 0 and var slash))
        {
            return SindriErrors.InvalidInput.With("this plan names no run area, so there is nothing to reproduce (fix: pass a launch whose sandbox-plan.json was written by Sindri)");
        }

        var area = file[..slash];
        var session = EnvValue("AW_SINDRI_HOME");
        var refs = "/refs/heads/" + RunNamespace;
        var gitDir = RunNamespace.Length == 0 ? null : Mounts.FirstOrDefault(m => m.Path.EndsWith(refs, StringComparison.Ordinal))?.Path[..^refs.Length];
        string Rebase(string path) => RebaseForExec(path, area, execArea, execLaunch, execNetDir);
        PlanMount Mount(PlanMount m) => m with { Path = Rebase(m.Path), Source = m.Source is { } source ? Rebase(source) : null, Kind = ReadOnlyWhenShared(m, area, gitDir) };

        return this with
        {
            Launch = execLaunch,
            Mounts = [.. Mounts.Where(m => session is null || !Under(m.Path, session)).Select(Mount)],
            Network = Network with { NetDir = execNetDir },
            Environment = [.. Environment.Where(e => e.Name is not ("AW_SINDRI_HOME" or "GH_CONFIG_DIR")).Select(e => e.Value is { } value ? e with { Value = Rebase(value) } : null).OfType<PlanEnvVar>()],   // a variable the launcher supplies (no value here) is not carried
            Github = GithubCredentials.None,
            Attended = true,   // the human drives it: no wall limit, and a full cap refuses at once
        };
    }

    // The stored launch's socket directory went with its egress; Seatbelt's short temp directory is per launch, so the exec gets its own, not the live agent's.
    private string RebaseForExec(string path, string area, string execArea, LaunchId execLaunch, string execNetDir)
    {
        var tempFrom = SandboxPaths.SeatbeltTemp(Launch);
        return Under(path, area) ? execArea + path[area.Length..]
            : Under(path, Network.NetDir) ? execNetDir + path[Network.NetDir.Length..]
            : Under(path, tempFrom) ? SandboxPaths.SeatbeltTemp(execLaunch) + path[tempFrom.Length..]
            : path;
    }

    private string? EnvValue(string name) => Environment.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.Ordinal))?.Value;

    private PlanMountKind ReadOnlyWhenShared(PlanMount m, string area, string? gitDir)
    {
        if (m.Kind != PlanMountKind.ReadWrite)
        {
            return m.Kind;
        }

        var shared = (m.Source is { } source && !Under(source, area))   // a host folder outside the run area, such as the owner's transcripts
            || (m.Source is null && ((gitDir is not null && Under(m.Path, gitDir)) || (Worktree is not null && Under(m.Path, Worktree)) || Under(m.Path, RunDirectory)));
        return shared ? PlanMountKind.ReadOnly : m.Kind;
    }

    private static bool Under(string path, string directory) =>
        string.Equals(path, directory, StringComparison.Ordinal) || path.StartsWith(directory + "/", StringComparison.Ordinal);
}
