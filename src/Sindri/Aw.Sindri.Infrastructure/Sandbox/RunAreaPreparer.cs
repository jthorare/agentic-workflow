using System.Text;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>
/// Makes the host side of a plan exist before bubblewrap starts (a bind needs its source): the per-launch and scratch directories, the empty global git config,
/// the refusing pre-push hook and the plan file ('sandbox explain' reads it; SB-5a) and the dotnet, npm and npx shims.
/// </summary>
internal static class RunAreaPreparer
{
    private static readonly string _refusingPrePush = "#!/bin/sh\necho 'git push is not available in a sandboxed session: Sindri sends pushes and pull requests for you. " + SandboxStatusLine.Refusal + "' >&2\nexit 1\n";

    /// <summary>Gets where the run area is: the directory of the plan file that AW_SANDBOX_PLAN names.</summary>
    /// <param name="plan">The plan.</param>
    /// <returns>The directory, or null when the plan names none.</returns>
    internal static string? RunAreaOf(SandboxPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return RunArea(plan);
    }

    /// <summary>
    /// Creates what the plan's required binds need and does not yet exist: scratch, spool and login directories, the run's ref directories and the read-write state the hooks share.
    /// The worktree and the run directory belong to the engine: a missing worktree is -015 and a missing run directory -016, never created here.
    /// </summary>
    /// <param name="plan">The plan.</param>
    /// <returns>The refusal, or null when the run area is ready.</returns>
    internal static Error? Prepare(SandboxPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.Worktree is { } worktree && !Directory.Exists(worktree))
        {
            return SandboxRefusal.For(SindriErrors.LaunchUnconfinable, SandboxReasons.WorktreeOutsideRepo, "the worktree does not exist: " + worktree);
        }

        if (!Directory.Exists(plan.RunDirectory))
        {
            return SandboxRefusal.For(SindriErrors.LaunchSelfCheckFailed, SandboxReasons.FsWrite, "the run directory does not exist: " + plan.RunDirectory);
        }

        var area = RunArea(plan);
        var engineOwned = new[] { plan.Worktree, plan.RunDirectory };
        foreach (var mount in plan.Mounts.Where(m => m is { Optional: false, Kind: PlanMountKind.ReadWrite or PlanMountKind.PerLaunchDir } && !engineOwned.Contains(m.Path, StringComparer.Ordinal)))
        {
            _ = Directory.CreateDirectory(mount.Source ?? mount.Path);
        }

        if (area is null)
        {
            return null;
        }

        var agentApp = plan.Github == GithubCredentials.AgentApp;
        _ = Directory.CreateDirectory(area + "/shims");
        WriteShims(plan, area + "/shims");
        if (agentApp)
        {
            WriteGithubShims(plan, area + "/shims");
        }

        _ = Directory.CreateDirectory(area + "/githooks");
        // why: the hook depends on the plan (agent-app allows the run branch), so a re-prepare may flip modes and must not keep the other mode's hook.
        WriteIfDifferent(area + "/githooks/pre-push", agentApp ? SandboxShims.BranchOnlyPrePush(plan.RunNamespace) : _refusingPrePush);
        WriteIfAbsent(area + "/gitconfig.empty", string.Empty, executable: false);
        OwnerOnly.WriteAtomically(Path.Combine(area, LaunchPaths.PlanName), AwJson.Serialize(plan, SandboxFilesJson.Options));   // value-free: launcher-supplied variables carry no value; 'sandbox explain' reads this file
        return null;
    }

    // why: the shims exec absolute paths, resolved here from the PATH the plan gives the box (its host part is bound, so a path found now is visible inside); heavy is the
    // `bin-r1b` entry the planner puts second, and only that one. No heavy means no shim at all (an exec of a missing wrapper would break every build), and a tool not on the host gets none:
    // the box then has no such tool, as before.
    private static void WriteShims(SandboxPlan plan, string shimsDirectory)
    {
        var entries = PathEntries(plan);
        var heavy = HeavyOf(entries);
        if (heavy is null)
        {
            return;
        }

        var hostDirectories = HostDirectories(plan, shimsDirectory);
        var real = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var tool in SandboxShims.Tools)
        {
            if (hostDirectories.Select(d => Path.Combine(d, tool)).FirstOrDefault(IsExecutable) is { } found)
            {
                real[tool] = found;
            }
        }

        SandboxShims.Write(shimsDirectory, real, heavy);
    }

    // why: the token file is in the launch's session home, which the plan carries as AW_SINDRI_HOME (rev 7); without it there is no token to point at, so no shim. gh needs no heavy,
    // and is looked up on the host PATH entries the same way the other tools are.
    private static void WriteGithubShims(SandboxPlan plan, string shimsDirectory)
    {
        if (plan.Environment.FirstOrDefault(e => string.Equals(e.Name, "AW_SINDRI_HOME", StringComparison.Ordinal))?.Value is not { Length: > 0 } sessionHome)
        {
            return;
        }

        var realGh = HostDirectories(plan, shimsDirectory).Select(d => Path.Combine(d, "gh")).FirstOrDefault(IsExecutable);
        SandboxShims.WriteGithub(shimsDirectory, realGh, SessionHomePaths.GhTokenFile(sessionHome));
    }

    private static string[] PathEntries(SandboxPlan plan) =>
        (plan.Environment.FirstOrDefault(e => string.Equals(e.Name, "PATH", StringComparison.Ordinal))?.Value ?? string.Empty).Split(':', StringSplitOptions.RemoveEmptyEntries);

    private static List<string> HostDirectories(SandboxPlan plan, string shimsDirectory)
    {
        var hostDirectories = PathEntries(plan).Where(e => !string.Equals(e, shimsDirectory, StringComparison.Ordinal) && !e.EndsWith("/bin-r1b", StringComparison.Ordinal)).ToList();
        if (plan.Environment.FirstOrDefault(e => string.Equals(e.Name, "DOTNET_ROOT", StringComparison.Ordinal))?.Value is { Length: > 0 } dotnetRoot)
        {
            hostDirectories.Add(dotnetRoot);
        }

        return hostDirectories;
    }

    // The planner's own bin-r1b entry, which the plan binds; a host PATH may carry another bin-r1b the box can't see.
    private static string? HeavyOf(string[] entries) =>
        entries.Length > 1 && entries[1].EndsWith("/bin-r1b", StringComparison.Ordinal) && IsExecutable(entries[1] + "/heavy") ? entries[1] + "/heavy" : null;

    private static bool IsExecutable(string path) =>
        File.Exists(path) && (OperatingSystem.IsWindows() || File.GetUnixFileMode(path).HasFlag(UnixFileMode.UserExecute));

    // The plan file sits in the run area, and AW_SANDBOX_PLAN names it.
    private static string? RunArea(SandboxPlan plan) =>
        plan.Environment.FirstOrDefault(e => string.Equals(e.Name, "AW_SANDBOX_PLAN", StringComparison.Ordinal))?.Value is { } file ? Path.GetDirectoryName(file) : null;

    private static void WriteIfDifferent(string path, string content)
    {
        if (File.Exists(path) && string.Equals(File.ReadAllText(path), content, StringComparison.Ordinal))
        {
            return;
        }

        if (File.Exists(path))
        {
            File.Delete(path);
        }

        WriteIfAbsent(path, content, executable: true);
    }

    private static void WriteIfAbsent(string path, string content, bool executable)
    {
        if (File.Exists(path))
        {
            return;
        }

        var mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | (executable ? UnixFileMode.UserExecute : UnixFileMode.None);
        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, mode);
        }
    }
}
