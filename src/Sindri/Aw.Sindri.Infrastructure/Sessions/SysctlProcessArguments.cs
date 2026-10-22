using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace Aw.Sindri.Infrastructure.Sessions;

/// <summary>The argument block of a process on macOS, from sysctl KERN_PROCARGS2 (the kernel gives a process's own user the block of its processes): argv[0] and the allowlisted environment, through <see cref="MacArgumentBlock"/>.</summary>
internal sealed partial class SysctlProcessArguments : IMacProcessArguments
{
    private const string LibSystem = "/usr/lib/libSystem.dylib";
    private const int KernelControl = 1;     // CTL_KERN
    private const int ArgumentMax = 8;       // KERN_ARGMAX
    private const int ProcessArguments = 49; // KERN_PROCARGS2

    public MacArguments? Read(int pid)
    {
        if (!OperatingSystem.IsMacOS() || ArgumentLimit() is not { } limit)
        {
            return null;
        }

        var buffer = ArrayPool<byte>.Shared.Rent(limit);   // why: one block per process of the table, each up to ARG_MAX: rented, not allocated, and cleared before it goes back so no other process's environment waits in the pool
        try
        {
            int[] request = [KernelControl, ProcessArguments, pid];
            var length = (nuint)limit;
            return Sysctl(ref request[0], 3, ref buffer[0], ref length, IntPtr.Zero, 0) == 0 ? MacArgumentBlock.Parse(buffer.AsSpan(0, (int)length)) : null;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }

    private static int? ArgumentLimit()
    {
        int[] request = [KernelControl, ArgumentMax];
        var size = new byte[sizeof(int)];
        var length = (nuint)sizeof(int);
        var limit = Sysctl(ref request[0], 2, ref size[0], ref length, IntPtr.Zero, 0) == 0 ? BinaryPrimitives.ReadInt32LittleEndian(size) : 0;
        return limit > sizeof(int) ? limit : null;
    }

    [LibraryImport(LibSystem, EntryPoint = "sysctl")]
    private static partial int Sysctl(ref int name, uint nameLength, ref byte value, ref nuint valueLength, IntPtr newValue, nuint newLength);
}
