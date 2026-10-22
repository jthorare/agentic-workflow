using System.Runtime.InteropServices;

namespace Aw.Sindri.Infrastructure.Sessions;

/// <summary>The uid this process runs as: the real uid from /proc/self/status on Linux (as the collector reads other processes'), getuid on macOS.</summary>
internal static partial class OwnUid
{
    /// <summary>The uid, or -1 when it can't be read (no process then counts as ours, and the collector reports proc-unreadable).</summary>
    internal static int Read() => OperatingSystem.IsLinux() ? ProcFiles.RealUid("/proc/self/status") ?? -1 : OperatingSystem.IsMacOS() ? (int)GetUid() : -1;

    [LibraryImport("/usr/lib/libSystem.dylib", EntryPoint = "getuid")]
    private static partial uint GetUid();
}
