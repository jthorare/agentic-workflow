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

    static Libc()
    {
        NativeLibrary.SetDllImportResolver(typeof(Libc).Assembly, Resolve);
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

    [LibraryImport(Library, EntryPoint = "kill", SetLastError = true)]
    private static partial int Kill(int pid, int signal);

    [LibraryImport(Library, EntryPoint = "mkdir", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int Mkdir(string path, uint mode);

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
