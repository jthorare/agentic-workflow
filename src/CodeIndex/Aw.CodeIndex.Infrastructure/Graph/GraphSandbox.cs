namespace Aw.CodeIndex.Infrastructure.Graph;

/// <summary>
/// Wraps a command so it runs with the network denied and writes confined to given folders (spec §5.5, §9); it fails closed: no sandbox,
/// no command. A copy of Context's wrapper with one named difference: read-only folders are bound read-only AFTER the credential
/// masks, so the pinned environment under the (masked) state directory stays visible. On macOS the matching allow comes after the deny.
/// </summary>
internal static class GraphSandbox
{
    private const string SandboxExec = "/usr/bin/sandbox-exec";
    private static readonly string[] _bwrap = ["/usr/bin/bwrap", "/bin/bwrap"];

    private static readonly string[] _secrets =
        [".ssh", ".aws", ".gnupg", ".agentic-workflow", ".config/gh", ".docker", ".kube", ".codex", ".claude", ".claude.json", ".netrc", ".git-credentials", ".npmrc", ".pypirc"];

    /// <summary>Wraps <paramref name="command"/>.</summary>
    /// <param name="host">The machine.</param>
    /// <param name="command">The command and its arguments.</param>
    /// <param name="writable">The only folders it may write.</param>
    /// <param name="readOnly">Folders it may read even when a credential mask covers them.</param>
    /// <returns>The argument vector, or null when this machine has no sandbox.</returns>
    public static IReadOnlyList<string>? Argv(GraphHost host, IReadOnlyList<string> command, IReadOnlyList<string> writable, IReadOnlyList<string> readOnly)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(writable);
        ArgumentNullException.ThrowIfNull(readOnly);
        if (host.IsMac && host.IsRootOwned(SandboxExec))
        {
            return [SandboxExec, "-p", MacProfile(host, writable, readOnly), .. command];
        }

        var bwrap = host.IsLinux ? _bwrap.FirstOrDefault(host.IsRootOwned) : null;
        return bwrap is null
            ? null
            : [bwrap, "--unshare-all", "--new-session", "--die-with-parent", "--ro-bind", "/", "/", "--dev", "/dev", "--proc", "/proc", "--tmpfs", "/tmp",
                .. LinuxMasks(host), .. readOnly.SelectMany(r => (string[])["--ro-bind", r, r]), .. writable.SelectMany(w => (string[])["--bind", w, w]), .. command];
    }

    private static List<string> LinuxMasks(GraphHost host)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var masks = new List<string>();
        string[] runtime = ["/run", "/var/run", .. host.RuntimeDir is { } dir && !dir.StartsWith("/run/", StringComparison.Ordinal) ? [dir] : Array.Empty<string>()];
        foreach (var path in runtime.Concat(_secrets.Select(d => $"{host.Home}/{d}")))
        {
            var kind = host.Kind(path);
            var real = host.RealPath(path);
            if (kind != PathKind.Missing && seen.Add(real))
            {
                masks.AddRange(kind == PathKind.Directory ? ["--tmpfs", real] : ["--ro-bind", "/dev/null", real]);
            }
        }

        return masks;
    }

    private static string MacProfile(GraphHost host, IReadOnlyList<string> writable, IReadOnlyList<string> readOnly)
    {
        static string Subpath(string path) => $"(subpath \"{path.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)}\")";
        var home = host.RealPath(host.Home);
        var hidden = _secrets.Append("Library/Keychains").SelectMany(d => (string[])[$"{home}/{d}", host.RealPath($"{home}/{d}")]).Distinct(StringComparer.Ordinal).Select(Subpath);
        var writes = writable.Select(Subpath).Append(Subpath("/dev"));
        var reads = readOnly.Count == 0 ? string.Empty : $"(allow file-read* {string.Join(' ', readOnly.Select(Subpath))})";
        return "(version 1)(allow default)(deny network*)(deny lsopen)(deny appleevent-send)"
            + "(deny mach-lookup (global-name \"com.apple.coreservices.launchservicesd\"))(deny process-exec (literal \"/bin/launchctl\"))"
            + $"(deny file-write*)(allow file-write* {string.Join(' ', writes)})(deny file-read* {string.Join(' ', hidden)}){reads}";
    }
}
