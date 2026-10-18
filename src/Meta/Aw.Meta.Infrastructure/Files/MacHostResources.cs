using Aw.Kernel;
using Aw.Meta.Core.Health;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Files;

/// <summary>
/// IHostResources on macOS, through <see cref="MacHostReader"/>: memory total from "sysctl -n hw.memsize", available memory from
/// vm_stat page counts, swap from "sysctl -n vm.swapusage", and the state disk. The heavy-job locks are the same directories as
/// on Linux. A tool that can't run or whose output can't be read is AW-META-010 naming it ("unavailable on macOS: ..."), never a made-up figure.
/// </summary>
/// <param name="reader">Runs and reads the tools.</param>
/// <param name="heavyLock">The heavy-job lock path (slot 1); the other slots are its siblings.</param>
/// <param name="liveness">Tells whether a lock holder still runs.</param>
internal sealed class MacHostResources(MacHostReader reader, string heavyLock, IProcessLiveness liveness) : IHostResources
{
    public async ValueTask<Result<HostResources>> Read(CancellationToken cancellationToken)
    {
        var total = await reader.TotalMemoryKb(cancellationToken).ConfigureAwait(false);
        if (total.IsError)
        {
            return total.Error;
        }

        var memory = await reader.Memory(cancellationToken).ConfigureAwait(false);
        return memory.IsError
            ? memory.Error
            : new HostResources(total.Value, memory.Value.AvailableKb, memory.Value.SwapUsedKb, memory.Value.DiskFreeBytes, memory.Value.SwapTotalKb);
    }

    public ValueTask<Result<IReadOnlyList<HeavyLock>>> HeavyLocks(CancellationToken cancellationToken) =>
        HeavyLockDirectory.List(heavyLock, liveness, cancellationToken);
}
