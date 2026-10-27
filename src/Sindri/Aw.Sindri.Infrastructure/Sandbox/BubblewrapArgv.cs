using Aw.Sindri.Core.Sandbox.Plan;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>
/// Turns a plan into bubblewrap arguments (sandbox spec §3.1, §4; spike items 1 and 2). Pure apart from the two lookups it is given:
/// what a host path is (a denied path is hidden by kind) and the value of a variable the plan leaves to the launcher (the launch secret, a model key).
/// </summary>
internal static class BubblewrapArgv
{
    private static readonly PlanMountKind[] _covering = [PlanMountKind.ReadOnly, PlanMountKind.ReadWrite, PlanMountKind.PerLaunchDir, PlanMountKind.Tmpfs];

    /// <summary>Gets the names of the variables the plan leaves to the launcher (the launch secret, a model key).</summary>
    /// <param name="plan">The plan.</param>
    /// <returns>The names.</returns>
    internal static IReadOnlyList<string> LauncherSupplied(SandboxPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return [.. plan.Environment.Where(e => e.Value is null).Select(e => e.Name)];
    }

    /// <summary>Builds the arguments that come before the agent's own argv (the caller adds the program name before and '--' after).</summary>
    /// <param name="plan">The plan.</param>
    /// <param name="host">What a host path is and where its symlinks lead.</param>
    /// <param name="secretsFd">True when the variables the plan leaves to the launcher arrive on file descriptor 3 (<c lang="text">--args 3</c>); no value is ever an argument.</param>
    /// <returns>The arguments.</returns>
    internal static IReadOnlyList<string> Build(SandboxPlan plan, IHostPaths host, bool secretsFd)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(host);
        List<string> a = ["--die-with-parent", "--unshare-net", "--clearenv"];   // no pid namespace (SB6); --new-session stays off, the tty remains the pane's
        for (var i = 0; i < plan.Mounts.Count; i++)
        {
            a.AddRange(Mount(plan.Mounts, i, host));
        }

        foreach (var e in plan.Environment)
        {
            if (e.Value is { } value)
            {
                a.AddRange(["--setenv", e.Name, value]);
            }
        }

        if (secretsFd)
        {
            a.AddRange(["--args", "3"]);   // after --clearenv, so the launcher's --setenv pairs land in the cleared environment
        }

        a.AddRange(["--chdir", plan.Worktree ?? plan.RunDirectory]);
        return a;
    }

    private static IEnumerable<string> Mount(IReadOnlyList<PlanMount> mounts, int index, IHostPaths host)
    {
        var m = mounts[index];
        var source = m.Source ?? m.Path;
        return m.Kind switch
        {
            PlanMountKind.ReadOnly => [m.Optional ? "--ro-bind-try" : "--ro-bind", source, m.Path],
            PlanMountKind.ReadWrite or PlanMountKind.PerLaunchDir => [m.Optional ? "--bind-try" : "--bind", source, m.Path],
            PlanMountKind.Dev => ["--dev", m.Path],
            PlanMountKind.Proc => ["--proc", m.Path],
            PlanMountKind.Tmpfs => Tmpfs(m),
            PlanMountKind.Deny => Deny(mounts, index, host),
            _ => throw new ArgumentOutOfRangeException(nameof(mounts), m.Kind, "unknown mount kind"),
        };
    }

    private static IEnumerable<string> Tmpfs(PlanMount m) =>
        m.Size is { } size ? ["--perms", "0700", "--size", size.ToString(System.Globalization.CultureInfo.InvariantCulture), "--tmpfs", m.Path] : ["--perms", "0700", "--tmpfs", m.Path];

    // A denied path is absent for the session. Under a tmpfs it already is. Elsewhere it is hidden by what it is: an empty tmpfs over a directory,
    // an empty read-only file over a file or socket, nothing when the host has none (a read then fails as absent).
    private static IEnumerable<string> Deny(IReadOnlyList<PlanMount> mounts, int index, IHostPaths host)
    {
        var path = mounts[index].Path;
        var cover = CoverOf(mounts, index, path);
        var real = host.Resolve(path);   // /var/run is /run: bubblewrap follows the link, so the tmpfs over /run hides /var/run/docker.sock as well
        if (cover is null || cover.Kind == PlanMountKind.Tmpfs || (!string.Equals(real, path, StringComparison.Ordinal) && CoverOf(mounts, index, real)?.Kind == PlanMountKind.Tmpfs))
        {
            return [];
        }

        var hostPath = (cover.Source ?? cover.Path).TrimEnd('/') + path[cover.Path.TrimEnd('/').Length..];
        return host.KindOf(hostPath) switch
        {
            HostPathKind.Directory => HasBindsBelow(mounts, index) ? ["--tmpfs", path] : ["--perms", "0555", "--tmpfs", path, "--remount-ro", path],
            HostPathKind.File => ["--ro-bind", "/dev/null", path],
            HostPathKind.Missing => [],
            _ => throw new ArgumentOutOfRangeException(nameof(host), "unknown host path kind"),
        };
    }

    // A directory that later entries bind into (the hook inventory inside the state directory) has to stay writable until they are made.
    private static bool HasBindsBelow(IReadOnlyList<PlanMount> mounts, int index) =>
        mounts.Skip(index + 1).Any(m => m.Kind != PlanMountKind.Deny && m.Path.StartsWith(mounts[index].Path + "/", StringComparison.Ordinal));

    private static PlanMount? CoverOf(IReadOnlyList<PlanMount> mounts, int index, string path) =>
        Enumerable.Range(0, index).Reverse().Select(i => mounts[i]).FirstOrDefault(m => _covering.Contains(m.Kind) && Covers(m.Path, path));

    private static bool Covers(string mount, string path) =>
        string.Equals(mount, "/", StringComparison.Ordinal) || string.Equals(mount, path, StringComparison.Ordinal) || path.StartsWith(mount + "/", StringComparison.Ordinal);
}
