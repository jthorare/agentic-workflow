using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Health;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Files;

/// <summary>
/// IHostResources on macOS: memory total from "sysctl -n hw.memsize", available memory from vm_stat page counts, swap from
/// "sysctl -n vm.swapusage", and the state disk. The heavy-job locks are the same directories as on Linux. A tool that
/// can't run or whose output can't be read is AW-META-010 naming it ("unavailable on macOS: ..."), never a made-up figure.
/// </summary>
/// <param name="stateDirectory">A directory on the disk whose free space is reported.</param>
/// <param name="heavyLock">The heavy-job lock path (slot 1); the other slots are its siblings.</param>
/// <param name="runner">Runs the tools.</param>
internal sealed class MacHostResources(string stateDirectory, string heavyLock, ICommandRunner runner) : IHostResources
{
    public async ValueTask<Result<HostResources>> Read(CancellationToken cancellationToken)
    {
        var total = await MacTools.Output(runner, ["sysctl", "-n", "hw.memsize"], cancellationToken).ConfigureAwait(false);
        var vm = await MacTools.Output(runner, ["vm_stat"], cancellationToken).ConfigureAwait(false);
        var swap = await MacTools.Output(runner, ["sysctl", "-n", "vm.swapusage"], cancellationToken).ConfigureAwait(false);
        if (total.IsError || vm.IsError || swap.IsError)
        {
            return total.IsError ? total.Error : vm.IsError ? vm.Error : swap.Error;
        }

        try
        {
            var (swapTotal, swapUsed) = MacFormat.SwapUsage(swap.Value);
            return new HostResources(MacFormat.MemTotalKb(total.Value), MacFormat.AvailableKb(vm.Value), swapUsed, DiskSpace.Free(stateDirectory), swapTotal);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException)
        {
            return MetaErrors.HostUnreadable.With("unavailable on macOS: " + exception.Message);
        }
    }

    public ValueTask<Result<IReadOnlyList<HeavyLock>>> HeavyLocks(CancellationToken cancellationToken) =>
        HeavyLockDirectory.List(heavyLock, cancellationToken);
}
