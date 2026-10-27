using System.Text;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>
/// Makes the host side of a plan exist before bubblewrap starts (a bind needs its source): the per-launch and scratch directories, the empty global git config,
/// the refusing pre-push hook and a placeholder plan file. Later steps fill the shims (SB-3b) and the plan file (SB-5a); this only makes the stack start.
/// </summary>
internal static class RunAreaPreparer
{
    private const string RefusingPrePush = "#!/bin/sh\necho 'git push is not available in a sandboxed session: Sindri sends pushes and pull requests for you.' >&2\nexit 1\n";

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

        _ = Directory.CreateDirectory(area + "/shims");
        _ = Directory.CreateDirectory(area + "/githooks");
        WriteIfAbsent(area + "/githooks/pre-push", RefusingPrePush, executable: true);
        WriteIfAbsent(area + "/gitconfig.empty", string.Empty, executable: false);
        WriteIfAbsent(area + "/sandbox-plan.json", "{}\n", executable: false);
        return null;
    }

    // The plan file sits in the run area, and AW_SANDBOX_PLAN names it.
    private static string? RunArea(SandboxPlan plan) =>
        plan.Environment.FirstOrDefault(e => string.Equals(e.Name, "AW_SANDBOX_PLAN", StringComparison.Ordinal))?.Value is { } file ? Path.GetDirectoryName(file) : null;

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
