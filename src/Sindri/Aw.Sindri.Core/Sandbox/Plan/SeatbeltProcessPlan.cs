namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>
/// The Seatbelt form of a plain process's plan (agent-first PR 16, #2489). The agent path's <see cref="SeatbeltPlan"/> needs a PlanContext only an agent launch builds; a process has no worktree,
/// no session home and no hooks, so its rewrite is small: the private /tmp (a bind on Linux) becomes the real folder under the run area, allowed after the deny of the state directory that holds it,
/// and HOME and TMPDIR point into it, because Seatbelt has no empty home to mount.
/// </summary>
public static class SeatbeltProcessPlan
{
    /// <summary>Rewrites a process plan built for Seatbelt.</summary>
    /// <param name="plan">The plan from <see cref="ProcessSandboxPlanner"/>.</param>
    /// <returns>The plan with the private /tmp moved after the denies and HOME and TMPDIR pointing into it.</returns>
    public static SandboxPlan Rewrite(SandboxPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var tmp = plan.Mounts.FirstOrDefault(IsPrivateTmp);
        if (tmp?.Source is not { } real)
        {
            return plan;   // no private /tmp: nothing to move
        }

        var home = real + "/home";
        List<PlanMount> mounts =
        [
            .. plan.Mounts.Where(m => !ReferenceEquals(m, tmp)),
            new PlanMount(real, PlanMountKind.ReadWrite, Reason: "private scratch and home, removed with the process's run area"),
        ];
        var environment = plan.Environment.Select(e => e.Name switch
        {
            "HOME" => e with { Value = home },
            "TMPDIR" => e with { Value = real },
            _ => e,
        }).ToList();
        return plan with { Mounts = mounts, Environment = environment, RunDirectory = real };
    }

    /// <summary>Tells whether a mount is the private /tmp bind of the process plan: the one read-write mount at /tmp that has a host source.</summary>
    /// <param name="m">A mount.</param>
    /// <returns>True for the private /tmp.</returns>
    public static bool IsPrivateTmp(PlanMount m) =>
        m is not null && m.Kind == PlanMountKind.ReadWrite && m.Source is not null && string.Equals(m.Path, "/tmp", StringComparison.Ordinal);
}
