using System.Reflection;
using System.Runtime.InteropServices;

namespace Aw.Sindri.Infrastructure.Locks;

/// <summary>
/// mkdir fails with EEXIST when the directory is there, which makes it the mutex the shell tools use (config/lib/locks.sh);
/// Directory.CreateDirectory succeeds on an existing directory, so it can't take a lock.
/// </summary>
internal static partial class Libc
{
    /// <summary>errno for "file exists" (the same value on macOS and Linux).</summary>
    internal const int AlreadyExists = 17;

    /// <summary>errno for "no such process" (the same value on macOS and Linux).</summary>
    internal const int NoSuchProcess = 3;

    /// <summary>errno for "operation not permitted": the process is there but belongs to someone else (the same value on macOS and Linux).</summary>
    internal const int NotPermitted = 1;

    private const string Library = "libc";

    private const int SigTtou = 22;   // the same number on macOS and Linux

    private static readonly nint _sigIgnore = 1;

    static Libc()
    {
        NativeLibrary.SetDllImportResolver(typeof(Libc).Assembly, Resolve);
    }

    /// <summary>errno for "function not implemented": this OS or file system has no atomic swap.</summary>
    internal const int NotImplemented = 38;   // Linux ENOSYS; macOS reports its own value (78), which only matters as "not zero"

    private const int AtFdCwd = -100;   // Linux AT_FDCWD
    private const uint SwapFlag = 2;    // Linux RENAME_EXCHANGE and macOS RENAME_SWAP are both 2

    /// <summary>Swaps two existing files in one step ('renameat2' with RENAME_EXCHANGE on Linux, 'renamex_np' with RENAME_SWAP on macOS): afterwards each path holds the other's file.</summary>
    /// <param name="first">One file.</param>
    /// <param name="second">The other file, on the same file system.</param>
    /// <returns>0 when swapped, else the errno (the file system or the OS has no swap, or a path is missing).</returns>
    internal static int SwapFiles(string first, string second)
    {
        if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux())
        {
            return NotImplemented;
        }

        try
        {
            var done = OperatingSystem.IsMacOS() ? RenamexNp(first, second, SwapFlag) : Renameat2(AtFdCwd, first, AtFdCwd, second, SwapFlag);
            return done == 0 ? 0 : Marshal.GetLastPInvokeError();
        }
        catch (EntryPointNotFoundException)
        {
            return NotImplemented;
        }
    }

    /// <summary>Creates one directory atomically.</summary>
    /// <param name="path">The directory; its parent must exist.</param>
    /// <returns>0, or the errno when it failed (<see cref="AlreadyExists"/> when it is there).</returns>
    internal static int MakeDirectory(string path) =>
        Mkdir(path, Convert.ToUInt32("777", 8)) == 0 ? 0 : Marshal.GetLastPInvokeError();

    /// <summary>Sends a signal to a process, or to a whole process group when <paramref name="pid"/> is negative.</summary>
    /// <param name="pid">The process id, or minus the process group id.</param>
    /// <param name="signal">The signal number; 0 only asks whether the target exists.</param>
    /// <returns>0, or the errno when it failed (<see cref="NoSuchProcess"/> when nothing is there).</returns>
    internal static int Signal(int pid, int signal) =>
        Kill(pid, signal) == 0 ? 0 : Marshal.GetLastPInvokeError();

    /// <summary>The process group id of this process.</summary>
    /// <returns>The group id.</returns>
    internal static int OwnGroup() => GetPgrp();

    /// <summary>Makes this process's group the terminal's foreground group again, when <paramref name="fromGroup"/> (a group that has ended) still holds it. Best effort: no terminal, or another foreground group, changes nothing.</summary>
    /// <param name="fromGroup">The process group that was given the terminal.</param>
    internal static void TakeForeground(int fromGroup)
    {
        try
        {
            using var tty = File.OpenHandle("/dev/tty", FileMode.Open, FileAccess.ReadWrite);
            var fd = (int)tty.DangerousGetHandle();
            if (TcGetPgrp(fd) != fromGroup)
            {
                return;
            }

            var previous = SetSignal(SigTtou, _sigIgnore);   // why: a background process that calls tcsetpgrp gets SIGTTOU, which stops it
            _ = TcSetPgrp(fd, GetPgrp());
            _ = SetSignal(SigTtou, previous);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // no controlling terminal (piped, --json, a service): nothing was handed over
        }
    }

    [LibraryImport(Library, EntryPoint = "getpgrp")]
    private static partial int GetPgrp();

    [LibraryImport(Library, EntryPoint = "kill", SetLastError = true)]
    private static partial int Kill(int pid, int signal);

    [LibraryImport(Library, EntryPoint = "tcgetpgrp")]
    private static partial int TcGetPgrp(int fd);

    [LibraryImport(Library, EntryPoint = "tcsetpgrp")]
    private static partial int TcSetPgrp(int fd, int pgrp);

    [LibraryImport(Library, EntryPoint = "signal")]
    private static partial nint SetSignal(int signal, nint handler);

    [LibraryImport(Library, EntryPoint = "mkdir", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int Mkdir(string path, uint mode);

    [LibraryImport(Library, EntryPoint = "renameat2", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int Renameat2(int oldDirectory, string oldPath, int newDirectory, string newPath, uint flags);

    [LibraryImport(Library, EntryPoint = "renamex_np", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int RenamexNp(string from, string to, uint flags);

    private static IntPtr Resolve(string name, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!string.Equals(name, Library, StringComparison.Ordinal))
        {
            return IntPtr.Zero;
        }

        return OperatingSystem.IsMacOS() ? NativeLibrary.Load("/usr/lib/libSystem.dylib")
            : OperatingSystem.IsLinux() ? NativeLibrary.Load("libc.so.6")
            : throw new PlatformNotSupportedException("the heavy-job lock needs macOS or Linux (the C library's mkdir)");
    }
}
