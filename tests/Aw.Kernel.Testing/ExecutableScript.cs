using System.ComponentModel;
using System.Diagnostics;

namespace Aw.Kernel.Testing;

/// <summary>Writes a shell script a test is about to run as a program. A fresh executable can fail to start with ETXTBSY ('text file busy') while a parallel test forks: the child inherits our write descriptor until it execs. Renaming does not help (same inode), so the script is run once, here, until it starts; after that no write descriptor is left and the code under test sees a normal executable.</summary>
public static class ExecutableScript
{
    private const int TextFileBusy = 26;   // ETXTBSY on Linux and macOS
    private const string WarmupVariable = "AW_TEST_SCRIPT_WARMUP";

    /// <summary>Writes <paramref name="body"/> under a shebang line and the warm-up guard, makes the file executable, runs it once and returns the path.</summary>
    /// <param name="path">Where the script goes.</param>
    /// <param name="body">The script after the shebang; the guard is added before it, so the warm-up run does nothing.</param>
    /// <param name="interpreter">The shebang's program; the guard is plain POSIX test syntax, so any shell works.</param>
    public static string Write(string path, string body, string interpreter = "/bin/sh")
    {
        var script = "#!" + interpreter + "\n[ -n \"$" + WarmupVariable + "\" ] && exit 0\n" + body;
        File.WriteAllText(path, script);
        if (OperatingSystem.IsWindows())
        {
            return path;   // no exec bit and no ETXTBSY there; the tests that run scripts need sh anyway
        }

        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        WarmUp(path);
        return path;
    }

    /// <summary>
    /// Starts <paramref name="info"/>, trying again while the exec fails with ETXTBSY ('text file busy'), for at most 100 tries 20 ms apart. For a script the code under test
    /// wrote and the test starts at once (a shim in a run area): a parallel test that forks while the write descriptor is still open makes the exec fail, and that is the same
    /// race <see cref="Write"/> closes for the files tests write. A failed exec never ran the script, so starting it again cannot run it twice. Any other start failure surfaces.
    /// </summary>
    /// <param name="info">What to start.</param>
    /// <returns>The started process.</returns>
    public static Process StartWhenNotBusy(ProcessStartInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return Process.Start(info)!;
            }
            catch (Win32Exception e) when (e.NativeErrorCode == TextFileBusy && attempt < 100)
            {
                RealTime.Sleep(TimeSpan.FromMilliseconds(20));   // only the exec-busy case is retried; any other start failure surfaces
            }
        }
    }

    private static void WarmUp(string path)
    {
        var info = new ProcessStartInfo(path) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        info.Environment[WarmupVariable] = "1";
        using var process = StartWhenNotBusy(info);
        process.WaitForExit();
    }
}
