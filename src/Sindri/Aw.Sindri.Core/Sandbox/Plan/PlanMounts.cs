using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>Builds the mount stack, bottom to top (sandbox spec §3.1): the root, an empty home, the allowlist, the agent home, the denies, the hook inventory, the writable set, the owner's last word.</summary>
internal static class PlanMounts
{
    private static readonly (string Path, PlanMountKind Kind, string Reason)[] _homeDefaults =
    [
        (".dotnet", PlanMountKind.ReadOnly, "the .NET SDK"),
        (".nvm", PlanMountKind.ReadOnly, "node versions"),
        (".cargo", PlanMountKind.ReadOnly, "Rust tools"),
        (".rustup", PlanMountKind.ReadOnly, "Rust toolchains"),
        (".local/bin", PlanMountKind.ReadOnly, "tools, and the judge wrapper"),
        (".nuget/packages", PlanMountKind.ReadWrite, "a restore writes new packages; shared with the owner's builds"),
        (".npm/_cacache", PlanMountKind.ReadWrite, "the npm cache; shared with the owner's builds"),
    ];

    internal static IReadOnlyList<PlanMount> Build(PlanContext c)
    {
        List<PlanMount> m =
        [
            new("/", PlanMountKind.ReadOnly, Reason: "the system, toolchains and other repos can be read, not written"),
            new("/dev", PlanMountKind.Dev, Reason: "a fresh /dev"),
            new("/proc", PlanMountKind.Proc, Reason: "a fresh /proc"),
            new("/tmp", PlanMountKind.ReadWrite, c.RunArea + "/tmp", Reason: "private scratch, removed with the run"),
            new(c.Home, PlanMountKind.Tmpfs, Size: c.Limits.HomeTmpfsBytes, Reason: "an empty home: only the entries below exist"),
        ];
        AddHomeAllow(m, c);
        AddLogin(m, c);
        AddMasks(m, c);
        m.AddRange(c.Denied().Select(d => new PlanMount(d.Path, PlanMountKind.Deny, Reason: d.Reason)));
        AddInventory(m, c);
        AddLaunchFiles(m, c);
        AddWritable(m, c);
        m.AddRange(c.OwnerDenied().Select(d => new PlanMount(d.Path, PlanMountKind.Deny, Reason: d.Reason)));
        return m;
    }

    private static void AddHomeAllow(List<PlanMount> m, PlanContext c)
    {
        m.AddRange(_homeDefaults.Select(d => new PlanMount($"{c.Home}/{d.Path}", d.Kind, Reason: d.Reason)));
        if (c.Request.Host.ToolkitDir.Length > 0)
        {
            m.Add(new PlanMount(c.Request.Host.ToolkitDir, PlanMountKind.ReadOnly, Reason: "the toolkit checkout the judge wrapper runs"));
        }

        IEnumerable<HomeAllowEntry> declared = [.. c.Request.Settings?.Fs?.HomeAllow ?? [], .. c.Repo.HomeAllow ?? []];
        m.AddRange(declared.Select(e => new PlanMount($"{c.Home}/{e.Path}", e.Mode == MountMode.ReadWrite ? PlanMountKind.ReadWrite : PlanMountKind.ReadOnly, Reason: "allowed by the profile")));
        m.AddRange((c.Repo.CachePaths ?? []).Select(p => new PlanMount($"{c.Home}/{p}", PlanMountKind.ReadWrite, Reason: "a cache path this repo declares")));
    }

    private static void AddLogin(List<PlanMount> m, PlanContext c)
    {
        if (c.Request.Login is not { } login)
        {
            return;
        }

        foreach (var mount in login.Mounts)
        {
            var kind = mount.Mode == MountMode.ReadWrite ? PlanMountKind.ReadWrite : PlanMountKind.ReadOnly;
            m.Add(new PlanMount(mount.SandboxPath, kind, string.Equals(mount.HostPath, mount.SandboxPath, StringComparison.Ordinal) ? null : mount.HostPath, Reason: $"the agent home, from the {SandboxWire.Name(c.Login)} login source"));
        }
    }

    private static void AddMasks(List<PlanMount> m, PlanContext c)
    {
        foreach (var path in new[] { c.Home + "/.claude/.credentials.json", c.Home + "/.codex/auth.json" })
        {
            if (m.Exists(x => Covers(x, path, c.Home)))
            {
                m.Add(new PlanMount(path, PlanMountKind.Deny, Reason: PlanContext.Always + "the owner's own login file; a session gets its login from its credential source"));
            }
        }
    }

    // The owner's real home is what is bound at its own path (no other host source); the root and the empty home don't count.
    private static bool Covers(PlanMount mount, string path, string home) =>
        mount.Kind is PlanMountKind.ReadOnly or PlanMountKind.ReadWrite
        && mount.Source is null
        && mount.Path.StartsWith(home + "/", StringComparison.Ordinal)
        && (string.Equals(mount.Path, path, StringComparison.Ordinal) || path.StartsWith(mount.Path + "/", StringComparison.Ordinal));

    private static void AddInventory(List<PlanMount> m, PlanContext c)
    {
        foreach (var entry in HookInventory.StatePaths.Where(e => e.Kind != StatePathKind.Absent))
        {
            m.Add(Inventory($"{c.StateDir}/{entry.Path}", entry, $"{c.RunArea}/spool/{entry.Path}"));
        }

        m.Add(new PlanMount(c.Request.Host.SindriHome + "/launches", PlanMountKind.PerLaunchDir, $"{c.RunArea}/spool/launches", Reason: "session-id-report writes here; thrown away with the run"));
    }

    private static PlanMount Inventory(string path, StatePathEntry entry, string spool)
    {
        if (entry.Kind == StatePathKind.PerLaunchDir)
        {
            return new PlanMount(path, PlanMountKind.PerLaunchDir, spool, Reason: entry.UsedBy);
        }

        return new PlanMount(path, entry.Kind == StatePathKind.ReadOnly ? PlanMountKind.ReadOnly : PlanMountKind.ReadWrite, Reason: entry.UsedBy);
    }

    private static void AddLaunchFiles(List<PlanMount> m, PlanContext c)
    {
        m.Add(new PlanMount("/run/sindri/net", PlanMountKind.ReadOnly, c.Request.Net.NetDir, Reason: "the sockets to Sindri and the egress proxy"));
        m.Add(new PlanMount(c.RunArea + "/shims", PlanMountKind.ReadOnly, Reason: "the dotnet, npm and npx shims that go through heavy"));
        m.Add(new PlanMount(c.RunArea + "/githooks", PlanMountKind.ReadOnly, Reason: "the refusing pre-push and the repo's own hooks"));
        m.Add(new PlanMount(c.RunArea + "/gitconfig.empty", PlanMountKind.ReadOnly, Reason: "an empty global git config"));
        m.Add(new PlanMount(c.RunArea + "/sandbox-plan.json", PlanMountKind.ReadOnly, Reason: "the value-free summary 'sandbox explain' reads"));
    }

    private static void AddWritable(List<PlanMount> m, PlanContext c)
    {
        var paths = c.Request.Paths;
        m.Add(new PlanMount(paths.RunDirectory, PlanMountKind.ReadWrite, Reason: "the Step's outputs"));
        if (paths.GitCommonDir.Length > 0)
        {
            m.Add(new PlanMount(paths.GitCommonDir, PlanMountKind.ReadOnly, Reason: "the git directory: config, hooks, packed-refs, tags and other runs' refs stay read-only"));
        }

        if (c.WritablePath is not { } worktree)
        {
            return;
        }

        m.Add(new PlanMount(worktree, PlanMountKind.ReadWrite, Reason: "the run's worktree"));
        if (paths.GitCommonDir.Length == 0)
        {
            return;
        }

        var git = paths.GitCommonDir;
        var ns = c.Request.RunNamespace;
        m.Add(new PlanMount(git + "/objects", PlanMountKind.ReadWrite, Reason: "a commit writes objects"));
        m.Add(new PlanMount($"{git}/worktrees/{worktree[(worktree.LastIndexOf('/') + 1)..]}", PlanMountKind.ReadWrite, Reason: "the worktree's own git state"));
        m.Add(new PlanMount($"{git}/refs/heads/{ns}", PlanMountKind.ReadWrite, Reason: "the run's ref directory: the branch lives one level inside it"));
        m.Add(new PlanMount($"{git}/logs/refs/heads/{ns}", PlanMountKind.ReadWrite, Reason: "the run's reflog directory"));
    }
}
