using System.Globalization;
using System.Text;

namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>
/// Renders a Seatbelt plan as an SBPL profile for 'sandbox-exec -p' (sandbox spec §3.1, §3.2, SB-9). Pure. SBPL applies the LAST rule that matches, so the profile replays the mount stack in order:
/// a deny followed by an allow under it renders in that order, and the owner's denies come last.
/// </summary>
public static class SeatbeltProfile
{
    private static readonly string[] _systemLinks = ["/var", "/tmp", "/etc"];

    /// <summary>Renders the profile.</summary>
    /// <param name="plan">A plan built for <see cref="SandboxMechanism.Seatbelt"/>.</param>
    /// <param name="runArea">The launch's run area: unix sockets may be bound under it (and under the launch's short temp directory).</param>
    /// <param name="canonical">Maps a path to the one Seatbelt matches; null uses <see cref="MacOsCanonical"/>. The adapter passes one that also resolves the symlinks it finds on the box.</param>
    /// <returns>The profile text.</returns>
    public static string Render(SandboxPlan plan, string runArea, Func<string, string>? canonical = null)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentException.ThrowIfNullOrEmpty(runArea);
        var path = canonical ?? MacOsCanonical;
        var sb = new StringBuilder();
        Header(sb, plan, runArea, path);
        List<(PlanMount Mount, string Path)> done = [];
        foreach (var mount in plan.Mounts)
        {
            if (mount.Kind is PlanMountKind.Link or PlanMountKind.Dev or PlanMountKind.Proc || string.Equals(mount.Path, "/", StringComparison.Ordinal))
            {
                continue;   // a link needs no rule (the rule is on its target), and the root is the default: readable, not writable
            }

            var real = path(mount.Path);
            Entry(sb, mount, real, done);
            done.Add((mount, real));
        }

        return sb.ToString();
    }

    /// <summary>macOS resolves /var, /tmp and /etc to /private/var, /private/tmp and /private/etc, and Seatbelt matches the resolved path.</summary>
    /// <param name="path">An absolute path.</param>
    /// <returns>The path with those three prefixes (the path itself or anything under it, not a longer name such as /variable) rewritten.</returns>
    public static string MacOsCanonical(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return _systemLinks.Any(p => string.Equals(path, p, StringComparison.Ordinal) || path.StartsWith(p + "/", StringComparison.Ordinal)) ? "/private" + path : path;
    }

    private static void Header(StringBuilder sb, SandboxPlan plan, string runArea, Func<string, string> canonical)
    {
        var net = plan.Network;
        _ = sb.AppendLine("(version 1)")
            .AppendLine("(allow default)")
            .AppendLine("; no network namespace on macOS: everything is denied, then only Sindri's port and the egress proxy's, on this machine")
            .AppendLine("(deny network*)")
            .AppendLine(Port(net.SindriPort))
            .AppendLine(Port(net.ProxyPort))
            .AppendLine(UnixSockets(plan, runArea, canonical))
            // why: denying ~/Library/Keychains is not enough, the keychain is read through securityd, so `security find-generic-password` would hand a Step the owner's login.
            .AppendLine("; the Keychain service: a Step never reads the owner's Keychain, its login comes from its credential source")
            .AppendLine("(deny mach-lookup (global-name \"com.apple.SecurityServer\") (global-name \"com.apple.securityd\"))")
            // Known gap, on purpose: loopback stays strict. A test that starts its own localhost server (Kestrel on :0, a dev server) can't run in a Mac Step,
            // because a Mac's loopback is shared with the owner's own services, serve included.
            .AppendLine("; nothing is writable until a rule below says so")
            .AppendLine("(deny file-write* (subpath \"/\"))")
            .AppendLine("; the terminal and the null device")
            .AppendLine("(allow file-write* (literal \"/dev/null\"))")
            .AppendLine("(allow file-write* (literal \"/dev/tty\"))")
            .AppendLine("(allow file-write* (regex #\"^/dev/ttys[0-9]+$\"))");
    }

    // why: (deny network*) also kills unix sockets, which MSBuild's worker nodes and the build server use (named pipes in TMPDIR); they may bind and connect under the run area and the launch's short temp directory only.
    private static string UnixSockets(SandboxPlan plan, string runArea, Func<string, string> canonical)
    {
        var areas = new[] { runArea, SandboxPaths.SeatbeltTemp(plan.Launch) }.Select(a => $"(subpath \"{Escape(canonical(a))}\")").ToList();
        var rules = areas.SelectMany(a => new[]
        {
            $"(allow network-bind (local unix-socket {a}))",
            $"(allow network-inbound (local unix-socket {a}))",
            $"(allow network-outbound (remote unix-socket {a}))",
        });
        return "; unix sockets under the run area and the launch's short temp directory only\n" + string.Join('\n', rules);
    }

    private static string Port(int port) => string.Create(CultureInfo.InvariantCulture, $"(allow network-outbound (remote ip \"localhost:{port}\"))");

    private static void Entry(StringBuilder sb, PlanMount mount, string path, List<(PlanMount Mount, string Path)> done)
    {
        var target = $"(subpath \"{Escape(path)}\")";
        var denied = done.Exists(d => d.Mount.Kind is PlanMountKind.Deny or PlanMountKind.Tmpfs && Under(path, d.Path));
        var writable = done.Exists(d => d.Mount.Kind is PlanMountKind.ReadWrite or PlanMountKind.PerLaunchDir && Under(path, d.Path));
        var rules = Rules(mount.Kind, target, denied, writable);
        if (rules.Count == 0)
        {
            return;
        }

        _ = sb.Append("; ").AppendLine(Comment(mount));
        foreach (var rule in rules)
        {
            _ = sb.AppendLine(rule);
        }
    }

    private static List<string> Rules(PlanMountKind kind, string target, bool denied, bool writable)
    {
        if (kind is PlanMountKind.Deny or PlanMountKind.Tmpfs)
        {
            return [$"(deny file-read* file-write* {target})"];
        }

        if (kind is PlanMountKind.ReadWrite or PlanMountKind.PerLaunchDir)
        {
            return [denied ? $"(allow file-read* file-write* {target})" : $"(allow file-write* {target})"];
        }

        List<string> rules = [];
        if (denied)
        {
            rules.Add($"(allow file-read* {target})");   // an earlier deny covers it
        }

        if (writable)
        {
            rules.Add($"(deny file-write* {target})");   // an earlier writable entry covers it
        }

        return rules;
    }

    private static string Comment(PlanMount mount) => (mount.Reason.Length > 0 ? mount.Reason : mount.Path).Replace('\n', ' ').Replace('\r', ' ');

    // SBPL string literals take the usual backslash escapes: a quote or a backslash in a path would otherwise end the string or start an escape.
    private static string Escape(string path) => path.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal).Replace("\r", "\\r", StringComparison.Ordinal);

    private static bool Under(string path, string directory) =>
        string.Equals(path, directory, StringComparison.Ordinal) || path.StartsWith(directory.TrimEnd('/') + "/", StringComparison.Ordinal);
}
