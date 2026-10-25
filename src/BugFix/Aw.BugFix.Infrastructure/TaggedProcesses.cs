using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace Aw.BugFix.Infrastructure;

/// <summary>
/// Finds and stops the processes one run started, whatever became of the process tree: every run's environment carries
/// AW_BUGFIX_RUN=&lt;token&gt;, and the environment survives setsid and a double fork, so a daemonized child (a fixture's
/// serve) is found where a tree kill misses it. Never a name pattern: only PIDs whose own environment holds the token.
/// Linux reads /proc/&lt;pid&gt;/environ, macOS asks ps; elsewhere nothing is found and the caller's tree kill is all there is.
/// </summary>
internal static partial class TaggedProcesses
{
    /// <summary>The variable that carries a run's token.</summary>
    internal const string Variable = "AW_BUGFIX_RUN";

    private const int Term = 15;
    private const int Kill = 9;
    private const string Library = "libc";
    private static readonly TimeSpan _poll = TimeSpan.FromMilliseconds(100);

    static TaggedProcesses()
    {
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            NativeLibrary.SetDllImportResolver(typeof(TaggedProcesses).Assembly, Resolve);
        }
    }

    /// <summary>
    /// SIGTERMs every process carrying <paramref name="token"/>, waits up to <paramref name="grace"/> for them to exit, then SIGKILLs
    /// what is left. Returns how many processes there were at the first look and how many needed the SIGKILL.
    /// </summary>
    internal static (int Found, int Killed) Stop(string token, TimeSpan grace)
    {
        var found = Find(token);
        if (found.Count == 0)
        {
            return (0, 0);
        }

        foreach (var pid in found)
        {
            Signal(pid, Term);
        }

        var deadline = Environment.TickCount64 + (long)grace.TotalMilliseconds;
        var left = Find(token);
        while (left.Count > 0 && Environment.TickCount64 < deadline)
        {
#pragma warning disable RS0030 // bugfix-state is a synchronous CLI; the wait is bounded by the grace period
            Thread.Sleep(_poll);
#pragma warning restore RS0030
            left = Find(token);
        }

        foreach (var pid in left)
        {
            Signal(pid, Kill);
        }

        return (found.Count, left.Count);
    }

    /// <summary>Every live process whose environment holds the token (this process excluded); empty where the OS offers no way to look.</summary>
    internal static IReadOnlyList<int> Find(string token)
    {
        var tagged = new List<int>();
        if (OperatingSystem.IsLinux())
        {
            var needle = $"\0{Variable}={token}\0";
            foreach (var directory in Directory.EnumerateDirectories("/proc"))
            {
                if (int.TryParse(Path.GetFileName(directory), NumberStyles.None, CultureInfo.InvariantCulture, out var pid) && pid != Environment.ProcessId && Carries(directory, needle))
                {
                    tagged.Add(pid);
                }
            }
        }
        else if (OperatingSystem.IsMacOS())
        {
            tagged.AddRange(PsTagged(token));
        }

        return tagged;
    }

    private static bool Carries(string procDirectory, string needle)
    {
        try
        {
            // a process that exited, a zombie (empty environ) or one that isn't ours reads as not carrying it.
            return $"\0{Encoding.Latin1.GetString(File.ReadAllBytes(Path.Combine(procDirectory, "environ")))}\0".Contains(needle, StringComparison.Ordinal);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    // ps -E appends each process's environment to its command; the token is unique, so a hit is one of ours.
    private static IEnumerable<int> PsTagged(string token)
    {
        var ps = Processes.Capture(Processes.Start("ps", ["-E", "-ww", "-o", "pid=,command=", "-U", Environment.UserName]), TimeSpan.FromSeconds(10));
        if (ps.ExitCode != 0)
        {
            yield break;   // no way to look: the tree kill is all there is.
        }

        var needle = $" {Variable}={token}";
        foreach (var line in ps.Output.Split('\n'))
        {
            var text = line.TrimStart();
            var space = text.IndexOf(' ', StringComparison.Ordinal);
            if (space > 0 && int.TryParse(text[..space], NumberStyles.None, CultureInfo.InvariantCulture, out var pid) && pid != Environment.ProcessId && text.Contains(needle, StringComparison.Ordinal))
            {
                yield return pid;
            }
        }
    }

    private static void Signal(int pid, int signal)
    {
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            _ = KillProcess(pid, signal);   // an ESRCH (it just exited) or EPERM (not ours) is nothing to act on
        }
    }

    [LibraryImport(Library, EntryPoint = "kill")]
    private static partial int KillProcess(int pid, int signal);

    private static IntPtr Resolve(string name, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!string.Equals(name, Library, StringComparison.Ordinal))
        {
            return IntPtr.Zero;
        }

        return NativeLibrary.Load(OperatingSystem.IsMacOS() ? "/usr/lib/libSystem.dylib" : "libc.so.6");
    }
}
