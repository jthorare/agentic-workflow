using System.Runtime.InteropServices;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>IPhysFootprint over libproc's proc_pid_rusage, flavor RUSAGE_INFO_V2 (macOS; the struct is the 19 words of rusage_info_v2, of which <c lang="text">ri_phys_footprint</c> is the 10th).</summary>
internal sealed partial class LibprocPhysFootprint : IPhysFootprint
{
    private const int InfoV2 = 2;

    /// <inheritdoc />
    public long? Sum(IReadOnlyList<int> pids)
    {
        ArgumentNullException.ThrowIfNull(pids);
        if (!OperatingSystem.IsMacOS())
        {
            return null;
        }

        long total = 0;
        var read = false;
        foreach (var pid in pids)
        {
            var info = default(RusageV2);
            if (ProcPidRusage(pid, InfoV2, ref info) == 0)   // why: a process that ended between ps and here is skipped; the others still count
            {
                total += (long)info.PhysFootprint;
                read = true;
            }
        }

        return read ? total : null;
    }

    [LibraryImport("/usr/lib/libproc.dylib", EntryPoint = "proc_pid_rusage")]
    private static partial int ProcPidRusage(int pid, int flavor, ref RusageV2 buffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct RusageV2
    {
        public ulong UuidLow;
        public ulong UuidHigh;
        public ulong UserTime;
        public ulong SystemTime;
        public ulong PkgIdleWakeups;
        public ulong InterruptWakeups;
        public ulong Pageins;
        public ulong WiredSize;
        public ulong ResidentSize;
        public ulong PhysFootprint;
        public ulong ProcStartAbsTime;
        public ulong ProcExitAbsTime;
        public ulong ChildUserTime;
        public ulong ChildSystemTime;
        public ulong ChildPkgIdleWakeups;
        public ulong ChildInterruptWakeups;
        public ulong ChildPageins;
        public ulong DiskIoBytesRead;
        public ulong DiskIoBytesWritten;
    }
}
