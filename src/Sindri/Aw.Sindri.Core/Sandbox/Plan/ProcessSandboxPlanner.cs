using System.Globalization;
using Aw.Kernel;

namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>The pure plan builder for a plain process (agent-first PR 16): the agent path's <see cref="SandboxPlanner"/> stays as it is.</summary>
public static class ProcessSandboxPlanner
{
    private const int PlanVersion = 1;
    private const long RunTmpfsBytes = 16L * 1024 * 1024;
    private static readonly string[] _proxyNames = ["HTTPS_PROXY", "HTTP_PROXY", "ALL_PROXY", "https_proxy", "http_proxy", "all_proxy"];
    private const string Always = "always blocked: ";

    /// <summary>Builds the plan for one process.</summary>
    /// <param name="mechanism">The mechanism of the calling adapter.</param>
    /// <param name="request">The process.</param>
    /// <returns>The plan, or SND-LAUNCH-015 when the command or a resource path is not absolute, a read-only path is also a resource, or a read-only path is /tmp.</returns>
    public static Result<SandboxPlan> Build(SandboxMechanism mechanism, ProcessSandboxRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!IsAbsolute(request.Command) || request.Command.LastIndexOf('/') == 0 || request.ResourceMounts.Any(r => !IsAbsolute(r.HostPath)) || (request.ReadOnlyPaths ?? []).Any(p => !IsAbsolute(p)) || !IsAbsolute(request.RunArea))
        {
            return SandboxRefusal.For(SindriErrors.LaunchUnconfinable, SandboxReasons.PathHidden, $"{request.Owner}: the command, its run area, every resource path and every read-only path must be absolute");
        }

        var conflict = ReadOnlyConflict(request);
        if (conflict is not null)
        {
            return SandboxRefusal.For(SindriErrors.LaunchUnconfinable, SandboxReasons.PathHidden, $"{request.Owner}: {conflict}");
        }

        var limits = LimitsResolver.Resolve(new LimitsInput(Limits: null, AttendedSettings: null, Attended: true, StepTimeout: null, LongMax: null, RamBytes: request.Host.TotalMemoryBytes, Cores: request.Host.CpuCores, Override: request.Limits));   // a toolkit is long-lived: no wall limit
        var plan = new SandboxPlan(
            PlanVersion,
            mechanism,
            string.Empty,
            Attended: false,
            request.Launch,
            Worktree: null,
            RunNamespace: string.Empty,
            RunDirectory: "/tmp",   // the cwd inside: the run area itself is not mounted, its private /tmp is
            Mounts(request, limits),
            new PlanNetwork(NetworkMode.Enforce, request.HostGroups, request.Net.SindriPort, request.Net.ProxyPort, request.Net.NetDir),
            Environment(request),
            limits,
            GithubCredentials.None,
            ModelLoginSource.OwnerCopy,   // no model runs here: the value only names the plan's login slot, which a process leaves empty
            InnerSandbox.Off,
            [],
            new InnerPolicy(InnerMode.Refuse, []),
            new PlanHooks([], [], []));
        return plan with { PlanHash = PlanHasher.Hash(plan, [("$RUN_AREA", request.RunArea), ("$NET", request.Net.NetDir), ("$LAUNCH", request.Launch.Value)]) };
    }

    // why: a read-only path that is also a resource has no right answer (writable or read-only?), and a read-only /tmp would hide the run's private /tmp: both are refused
    private static string? ReadOnlyConflict(ProcessSandboxRequest r)
    {
        var resources = r.ResourceMounts.Select(static x => x.HostPath.TrimEnd('/')).ToHashSet(StringComparer.Ordinal);
        foreach (var path in (r.ReadOnlyPaths ?? []).Select(static x => x.TrimEnd('/')))
        {
            if (string.Equals(path, "/tmp", StringComparison.Ordinal))
            {
                return "a read-only /tmp would hide the run's private /tmp";
            }

            if (resources.Contains(path))
            {
                return $"{path} is both a resource and a read-only path; name it once";
            }
        }

        return null;
    }

    // why: the command also needs a parent folder ("/tool" has none), because that folder is bound read-only as the binary's; an empty source would mount nothing
    private static bool IsAbsolute(string path) => path.Length > 1 && path[0] == '/';

    private static List<PlanMount> Mounts(ProcessSandboxRequest r, PlanLimits limits)
    {
        var host = r.Host;
        var home = host.Home;
        List<PlanMount> m =
        [
            new("/", PlanMountKind.ReadOnly, Reason: "the system and toolchains can be read, not written"),
            new("/dev", PlanMountKind.Dev, Reason: "a fresh /dev"),
            new("/proc", PlanMountKind.Proc, Reason: "a fresh /proc"),
            new("/run", PlanMountKind.Tmpfs, Size: RunTmpfsBytes, Reason: "an empty /run: the user sockets and the Docker socket are gone"),
            new("/tmp", PlanMountKind.ReadWrite, r.RunArea + "/tmp", Reason: "private scratch, removed with the process's run area"),
            new(home, PlanMountKind.Tmpfs, Size: limits.HomeTmpfsBytes, Reason: "an empty home: nothing of the owner's is visible"),
        ];
        var dotnet = host.Environment.TryGetValue("DOTNET_ROOT", out var root) ? root.TrimEnd('/') : string.Empty;
        if (dotnet.Length > 1)
        {
            m.Add(new PlanMount(dotnet, PlanMountKind.ReadOnly, Reason: "DOTNET_ROOT, for a framework-dependent binary", Optional: true));
        }

        m.Add(new PlanMount(host.StateDir, PlanMountKind.Deny, Reason: Always + "the ledger, profile, tokens and other runs' areas live here"));
        if (!string.Equals(host.SindriHome, host.StateDir, StringComparison.Ordinal))
        {
            m.Add(new PlanMount(host.SindriHome, PlanMountKind.Deny, Reason: Always + "Sindri's own state"));
        }

        m.Add(new PlanMount("/var/run/docker.sock", PlanMountKind.Deny, Reason: Always + "the Docker socket is root on the host"));
        m.Add(new PlanMount("/run/podman", PlanMountKind.Deny, Reason: Always + "the Podman socket is root on the host"));
        m.Add(new PlanMount("/run/user/" + host.Uid.ToString(CultureInfo.InvariantCulture), PlanMountKind.Deny, Reason: Always + "the ssh, gpg and dbus agent sockets sign as the owner"));
        m.Add(new PlanMount("/etc/ssh", PlanMountKind.Deny, Reason: Always + "host keys"));
        m.Add(new PlanMount("/root", PlanMountKind.Deny, Reason: Always + "not the process's"));
        m.AddRange(host.OtherHomes.Select(h => new PlanMount(h, PlanMountKind.Deny, Reason: Always + "other users' files")));

        m.AddRange(Resources(r));
        if (r.HostGroups.Count > 0)
        {
            m.Add(new PlanMount("/run/sindri/net", PlanMountKind.ReadOnly, r.Net.NetDir, Reason: "the sockets to the egress proxy: the only way out"));
        }

        return m;
    }

    // why: above the denies, because the binary and the resources live under Sindri's home. bwrap applies mounts in order, so the list is sorted by depth, whatever the kind:
    // an ancestor comes first, a writable resource is never shadowed by a read-only ancestor, and a read-only path under a resource is mounted after it and stays read-only.
    // Within one depth the order is the binary, the resources, then the read-only paths (the sort is stable), so a read-only path equal to a resource
    // is refused in Build (#2815), as is a read-only /tmp, so no two mounts share a path here.
    private static IEnumerable<PlanMount> Resources(ProcessSandboxRequest r)
    {
        var binary = r.Command[..r.Command.LastIndexOf('/')];
        var paths = (r.ReadOnlyPaths ?? []).Select(static x => x.TrimEnd('/')).Where(static x => x.Length > 0).Distinct(StringComparer.Ordinal).Where(x => !string.Equals(x, binary, StringComparison.Ordinal));
        return new[] { new PlanMount(binary, PlanMountKind.ReadOnly, Reason: $"the binary of {r.Owner}") }
            .Concat(r.ResourceMounts.Select(x => new PlanMount(x.HostPath.TrimEnd('/'), PlanMountKind.State, Reason: $"the {x.Name} resource of {r.Owner}")))
            .Concat(paths.Select(x => new PlanMount(x, PlanMountKind.ReadOnly, Reason: $"read-only for {r.Owner}")))
            .OrderBy(static x => x.Path.Count(static c => c == '/'));
    }

    private static List<PlanEnvVar> Environment(ProcessSandboxRequest r)
    {
        var host = r.Host.Environment;
        string FromHost(string name, string fallback) => host.TryGetValue(name, out var v) && v.Length > 0 ? v : fallback;
        List<PlanEnvVar> env =
        [
            new("PATH", FromHost("PATH", "/usr/local/bin:/usr/bin:/bin"), "the tools a process may call"),
            new("HOME", r.Host.Home, "an empty home"),
            new("LANG", FromHost("LANG", "C.UTF-8"), "the locale"),
            new("TMPDIR", "/tmp", "the private scratch"),
            new("DOTNET_NOLOGO", "1", "no first-run banner"),
            new("DOTNET_CLI_TELEMETRY_OPTOUT", "1", "no telemetry"),
        ];
        if (host.TryGetValue("DOTNET_ROOT", out var root) && root.Length > 0)
        {
            env.Add(new PlanEnvVar("DOTNET_ROOT", root, "the runtime of a framework-dependent binary"));
        }

        if (r.HostGroups.Count > 0)
        {
            var url = string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{r.Net.ProxyPort}");
            env.AddRange(_proxyNames.Select(n => new PlanEnvVar(n, url, "the egress proxy is the only way out")));
            env.Add(new PlanEnvVar("NO_PROXY", "127.0.0.1,localhost,::1", "the proxy itself is local"));
        }

        env.AddRange(r.Environment.OrderBy(e => e.Key, StringComparer.Ordinal).Select(e => new PlanEnvVar(e.Key, e.Value, "a declared resource")));
        return env;
    }
}
