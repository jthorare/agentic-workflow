using System.Diagnostics;

namespace Aw.CodeIndex.Infrastructure.Graph;

/// <summary>The machine the sandbox command is built for (a copy of Context's SandboxHost; Context is another package and keeps its own internal).</summary>
/// <param name="IsLinux">Linux (bwrap).</param>
/// <param name="IsMac">macOS (sandbox-exec).</param>
/// <param name="Home">The user's home: its credential stores are hidden.</param>
/// <param name="RuntimeDir">$XDG_RUNTIME_DIR, or null.</param>
/// <param name="IsRootOwned">Whether an absolute path is a root-owned executable file.</param>
/// <param name="Kind">What a path is.</param>
/// <param name="RealPath">A path with its links resolved, or the path itself when it does not exist.</param>
internal sealed record GraphHost(bool IsLinux, bool IsMac, string Home, string? RuntimeDir, Func<string, bool> IsRootOwned, Func<string, PathKind> Kind, Func<string, string> RealPath)
{
    /// <summary>This machine.</summary>
    /// <returns>The host.</returns>
    public static GraphHost Current() => new(
        OperatingSystem.IsLinux(), OperatingSystem.IsMacOS(), Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR"),
        RootOwnedExecutable,
        path => Directory.Exists(path) ? PathKind.Directory : File.Exists(path) ? PathKind.File : PathKind.Missing,
        Resolve);

    private static string Resolve(string path)
    {
        try
        {
            var full = Path.GetFullPath(path);
            var info = Directory.Exists(full) ? (FileSystemInfo)new DirectoryInfo(full) : new FileInfo(full);
            return info.ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? ResolveParent(full);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return path;
        }
    }

    // A link above the last component (macOS /tmp -> /private/tmp) is resolved through the parent.
    private static string ResolveParent(string full)
    {
        var parent = Path.GetDirectoryName(full);
        if (parent is null || string.Equals(parent, full, StringComparison.Ordinal))
        {
            return full;
        }

        var resolved = new DirectoryInfo(parent).ResolveLinkTarget(returnFinalTarget: true)?.FullName;
        return resolved is null ? full : Path.Combine(resolved, Path.GetFileName(full));
    }

    // stat's owner column through the system's own /usr/bin/stat: .NET has no uid API.
    private static bool RootOwnedExecutable(string path)
    {
        if (OperatingSystem.IsWindows() || !File.Exists(path) || !File.GetUnixFileMode(path).HasFlag(UnixFileMode.OtherExecute))
        {
            return false;
        }

        try
        {
            var start = new ProcessStartInfo("/usr/bin/stat") { RedirectStandardOutput = true, UseShellExecute = false };
            foreach (var arg in OperatingSystem.IsMacOS() ? (string[])["-f", "%u", path] : ["-c", "%u", path])
            {
                start.ArgumentList.Add(arg);
            }

            using var stat = Process.Start(start);
            var owner = stat?.StandardOutput.ReadToEnd().Trim();
            stat?.WaitForExit();
            return owner == "0" && stat?.ExitCode == 0;
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }
}
