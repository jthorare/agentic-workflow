using System.Diagnostics;
using Aw.Kernel.Data.Processes;

namespace Aw.Context.Infrastructure.Extract;

/// <summary>The machine the sandbox command is built for.</summary>
/// <param name="IsLinux">Linux (bwrap).</param>
/// <param name="IsMac">macOS (sandbox-exec).</param>
/// <param name="Home">The user's home: its credential stores are hidden.</param>
/// <param name="RuntimeDir">$XDG_RUNTIME_DIR, or null.</param>
/// <param name="IsRootOwned">Whether an absolute path is a root-owned executable file (a tool a user install dropped on PATH never counts).</param>
/// <param name="Kind">What a path is.</param>
/// <param name="RealPath">A path with its links resolved, or the path itself when it doesn't exist.</param>
internal sealed record SandboxHost(bool IsLinux, bool IsMac, string Home, string? RuntimeDir, Func<string, bool> IsRootOwned, Func<string, PathKind> Kind, Func<string, string> RealPath)
{
    /// <summary>This machine.</summary>
    /// <returns>The host.</returns>
    public static SandboxHost Current() => new(
        OperatingSystem.IsLinux(), OperatingSystem.IsMacOS(), Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR"),
        RootOwnedExecutable,
        path => Directory.Exists(path) ? PathKind.Directory : File.Exists(path) ? PathKind.File : PathKind.Missing,
        path => Map.RealPath.Of(path) ?? path);

    // stat's owner column through the system's own /usr/bin/stat: .NET has no uid API, and a P/Invoke stat struct differs per platform.
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

            using var pinned = PinnedProcess.Start(start);
            var stat = pinned.Process;
            var owner = stat.StandardOutput.ReadToEnd().Trim();
            stat.WaitForExit();
            return owner == "0" && stat.ExitCode == 0;
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }
}
