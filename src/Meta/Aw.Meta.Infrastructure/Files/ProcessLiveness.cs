using System.Globalization;

namespace Aw.Meta.Infrastructure.Files;

/// <summary>Whether a process id belongs to a running process: its /proc entry on Linux, the OS process list elsewhere (macOS).</summary>
internal static class ProcessLiveness
{
    /// <summary>Whether the pid is a running process (a zombie counts as running until it is reaped).</summary>
    /// <param name="pid">The process id.</param>
    /// <returns>True when the process exists.</returns>
    internal static bool IsAlive(int pid)
    {
        if (pid <= 0)
        {
            return false;
        }

        if (Directory.Exists("/proc/self"))
        {
            return Directory.Exists("/proc/" + pid.ToString(CultureInfo.InvariantCulture));
        }

        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }
}
