using Aw.Kernel;
using Aw.Meta.Core.Health;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Files;

/// <summary>
/// IProcessTable on macOS, through <see cref="MacHostReader"/>: the processes from "ps -axww -o pid=,ppid=,etime=,rss=,stat=,args="
/// (age from elapsed time, so no time-zone or boot-time arithmetic), memory from vm_stat and sysctl, the state disk. Zombies are
/// left out. Termination is SIGTERM by pid, through the kill program.
/// </summary>
/// <param name="reader">Runs and reads the tools.</param>
internal sealed class MacProcessTable(MacHostReader reader) : IProcessTable
{
    public async ValueTask<Result<HostSnapshot>> Snapshot(CancellationToken cancellationToken)
    {
        var processes = await reader.Processes(cancellationToken).ConfigureAwait(false);
        if (processes.IsError)
        {
            return processes.Error;
        }

        var memory = await reader.Memory(cancellationToken).ConfigureAwait(false);
        return memory.IsError
            ? memory.Error
            : new HostSnapshot(processes.Value, memory.Value.AvailableKb, memory.Value.SwapUsedKb, memory.Value.DiskFreeBytes);
    }

    public ValueTask<Result<Unit>> Terminate(int pid, CancellationToken cancellationToken) =>
        SignalProcess.Refusal(pid) is { } refusal ? ValueTask.FromResult(Results.Failure<Unit>(refusal)) : reader.Terminate(pid, cancellationToken);
}
