using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Sandbox.Credentials;

namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>Builds the mount stack, bottom to top (sandbox spec §3.1): the root, an empty home, the allowlist, the agent home, the denies, the hook inventory, the writable set, the owner's last word.</summary>
internal static class PlanMounts
{
    private const long MaskBytes = 1024 * 1024;

    private static readonly (string Path, PlanMountKind Kind, string Reason)[] _homeDefaults =
    [
        (".dotnet", PlanMountKind.ReadOnly, "the .NET SDK"),
        (".cargo", PlanMountKind.ReadOnly, "Rust tools"),
        (".rustup", PlanMountKind.ReadOnly, "Rust toolchains"),
        (".local/bin", PlanMountKind.ReadOnly, "tools, and the sindri and judge wrappers"),
        (".nuget/packages", PlanMountKind.ReadWrite, "a restore writes new packages; shared with the owner's builds"),
        (".npm/_cacache", PlanMountKind.ReadWrite, "the npm cache; shared with the owner's builds"),
    ];

    private const long RunTmpfsBytes = 16L * 1024 * 1024;

    internal static IReadOnlyList<PlanMount> Build(PlanContext c)
    {
        List<PlanMount> m =
        [
            new("/", PlanMountKind.ReadOnly, Reason: "the system, toolchains and other repos can be read, not written"),
            new("/dev", PlanMountKind.Dev, Reason: "a fresh /dev"),
            new("/proc", PlanMountKind.Proc, Reason: "a fresh /proc"),
            new("/run", PlanMountKind.Tmpfs, Size: RunTmpfsBytes, Reason: "an empty /run: the user sockets and the Docker socket are gone, and the net directory has a place to bind"),
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
        m.AddRange(_homeDefaults.Select(d => new PlanMount($"{c.Home}/{d.Path}", d.Kind, Reason: d.Reason, Optional: true)));
        IEnumerable<HomeAllowEntry> declared = [.. c.Request.Settings?.Fs?.HomeAllow ?? [], .. c.Repo.HomeAllow ?? []];
        m.AddRange(declared.Select(e => new PlanMount($"{c.Home}/{e.Path}", e.Mode == MountMode.ReadWrite ? PlanMountKind.ReadWrite : PlanMountKind.ReadOnly, Reason: "allowed by the profile", Optional: true)));
        m.AddRange((c.Repo.CachePaths ?? []).Select(p => new PlanMount($"{c.Home}/{p}", PlanMountKind.ReadWrite, Reason: "a cache path this repo declares", Optional: true)));
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

        AddTranscripts(m, c, login);
    }

    // The CLI writes under its config directory, which is a per-launch copy; only this worktree's transcripts go back to the owner's real 'projects/<key>', so
    // Sindri's readers and a resume find them. The memory directory inside stays the owner's (spec §3.1).
    private static void AddTranscripts(List<PlanMount> m, PlanContext c, PreparedLogin login)
    {
        if (c.Worktree is not { } worktree || !login.Environment.TryGetValue("CLAUDE_CONFIG_DIR", out var config))
        {
            return;
        }

        var key = ProjectKey.For(worktree);
        var real = $"{c.Home}/.claude/projects/{key}";
        m.Add(new PlanMount($"{config}/projects/{key}", PlanMountKind.ReadWrite, real, Reason: "this worktree's transcripts, in the owner's agent home"));
        m.Add(new PlanMount($"{config}/projects/{key}/memory", PlanMountKind.ReadOnly, real + "/memory", Reason: "the owner's memory for this project stays read-only", Optional: true));
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

        AddBinaries(m, c);
        AddSindriHome(m, c);
    }

    // The state directory is otherwise absent, so the wrappers in ~/.local/bin would fail at exec without the stamp; 'current' is a sibling link the sandbox does not have.
    private static void AddBinaries(List<PlanMount> m, PlanContext c)
    {
        if (c.Request.Host.CsBinDir.Length > 0)
        {
            m.Add(new PlanMount(c.Request.Host.CsBinDir, PlanMountKind.ReadOnly, Reason: "the installed C# sindri and judge"));
            m.Add(new PlanMount(c.StateDir + "/bin/cs/current", PlanMountKind.ReadOnly, c.Request.Host.CsBinDir, Reason: "the wrappers exec into 'current': resolved to the stamp at plan time"));
        }

        var dotnet = c.Request.Host.Environment.TryGetValue("DOTNET_ROOT", out var root) ? root : string.Empty;
        if (dotnet.Length > 0 && !string.Equals(dotnet, c.Home + "/.dotnet", StringComparison.Ordinal))
        {
            m.Add(new PlanMount(dotnet, PlanMountKind.ReadOnly, Reason: "DOTNET_ROOT, the native binaries' runtime"));
        }
    }

    // Rev 7: the real home and the other launches' session homes are masked so the mountpoints exist, then only what the session home's links need is bound, shallow to deep.
    private static void AddSindriHome(List<PlanMount> m, PlanContext c)
    {
        var home = c.Request.Host.SindriHome;
        var session = c.SessionHome;
        m.Add(new PlanMount(home, PlanMountKind.Tmpfs, Size: MaskBytes, Reason: "Sindri's real home is empty here: only the entries below exist"));
        m.Add(new PlanMount(SessionHomePaths.Sessions(home), PlanMountKind.Tmpfs, Size: MaskBytes, Reason: "other launches' session homes are absent"));
        m.Add(new PlanMount(home + "/launches", PlanMountKind.ReadWrite, Reason: "the session home's launches link resolves here; session-id-report and the turn-ended marker write to it"));
        m.Add(new PlanMount(home + "/approved", PlanMountKind.ReadOnly, Reason: "the session home's approved link resolves here: the CLI finds its repo in the approved profile snapshot"));
        m.Add(new PlanMount(session, PlanMountKind.ReadOnly, Reason: "this launch's session home: the Step's policy file and MCP config; the agent can't rewrite its own policy"));
        m.Add(new PlanMount(session + "/control.json", PlanMountKind.ReadOnly, Reason: "written by the launcher: the endpoint record with the forwarder's URL"));
        m.Add(new PlanMount(session + "/gh-config", PlanMountKind.ReadWrite, Reason: "the GitHub CLI's empty config directory, so gh finds no login"));
    }

    private static PlanMount Inventory(string path, StatePathEntry entry, string spool)
    {
        if (entry.Kind == StatePathKind.PerLaunchDir)
        {
            return new PlanMount(path, PlanMountKind.PerLaunchDir, spool, Reason: entry.UsedBy);
        }

        return new PlanMount(path, entry.Kind == StatePathKind.ReadOnly ? PlanMountKind.ReadOnly : PlanMountKind.ReadWrite, Reason: entry.UsedBy, Optional: entry.Kind == StatePathKind.ReadOnly);   // a read-write state directory is made, never skipped: a session with a private locks/ or judge/ shares nothing with the box
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
        m.Add(new PlanMount(paths.GitCommonDir, PlanMountKind.ReadOnly, Reason: "the git directory: config, hooks, packed-refs, tags and other runs' refs stay read-only"));   // why: PlanChecks proved it is a clean path inside the repo

        if (c.WritablePath is not { } worktree)
        {
            return;
        }

        m.Add(new PlanMount(worktree, PlanMountKind.ReadWrite, Reason: "the run's worktree"));
        var git = paths.GitCommonDir;
        var ns = c.Request.RunNamespace;
        m.Add(new PlanMount(git + "/objects", PlanMountKind.ReadWrite, Reason: "a commit writes objects"));
        m.Add(new PlanMount($"{git}/worktrees/{worktree[(worktree.LastIndexOf('/') + 1)..]}", PlanMountKind.ReadWrite, Reason: "the worktree's own git state"));
        m.Add(new PlanMount($"{git}/refs/heads/{ns}", PlanMountKind.ReadWrite, Reason: "the run's ref directory: the branch lives one level inside it"));
        m.Add(new PlanMount($"{git}/logs/refs/heads/{ns}", PlanMountKind.ReadWrite, Reason: "the run's reflog directory"));
    }
}
