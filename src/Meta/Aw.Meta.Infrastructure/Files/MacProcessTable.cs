using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Health;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Files;

/// <summary>
/// IProcessTable on macOS: the processes from "ps -axww -o pid=,ppid=,etime=,rss=,stat=,args=" (age from elapsed time, so no
/// time-zone or boot-time arithmetic), memory from vm_stat and sysctl, the state disk. Zombies are left out. Termination is
/// SIGTERM by pid, through the kill program, as on Linux.
/// </summary>
/// <param name="stateDirectory">A directory on the disk whose free space is reported.</param>
/// <param name="runner">Runs the tools.</param>
internal sealed class MacProcessTable(string stateDirectory, ICommandRunner runner) : IProcessTable
{
    public async ValueTask<Result<HostSnapshot>> Snapshot(CancellationToken cancellationToken)
    {
        var ps = await MacTools.Output(runner, ["ps", "-axww", "-o", "pid=,ppid=,etime=,rss=,stat=,args="], cancellationToken).ConfigureAwait(false);
        var vm = await MacTools.Output(runner, ["vm_stat"], cancellationToken).ConfigureAwait(false);
        var swap = await MacTools.Output(runner, ["sysctl", "-n", "vm.swapusage"], cancellationToken).ConfigureAwait(false);
        if (ps.IsError || vm.IsError || swap.IsError)
        {
            return ps.IsError ? ps.Error : vm.IsError ? vm.Error : swap.Error;
        }

        try
        {
            return new HostSnapshot(MacFormat.Processes(ps.Value), MacFormat.AvailableKb(vm.Value), MacFormat.SwapUsage(swap.Value).UsedKb, DiskSpace.Free(stateDirectory));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException)
        {
            return MetaErrors.HostUnreadable.With("unavailable on macOS: " + exception.Message);
        }
    }

    public ValueTask<Result<Unit>> Terminate(int pid, CancellationToken cancellationToken) => SignalProcess.Terminate(pid, cancellationToken);
}
