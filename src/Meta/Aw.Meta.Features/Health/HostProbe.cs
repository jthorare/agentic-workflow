using Aw.Kernel;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.Health;

/// <summary>Reads the host: processes, memory and disk, heavy-job locks, systemd's OOM policy.</summary>
/// <param name="processes">The process table.</param>
/// <param name="resources">Memory, disk and locks.</param>
/// <param name="oom">Reads systemd's OOM policy for the advisory line.</param>
/// <param name="stale">Compares the installed heavy scripts with the checkout's for the advisory line.</param>
internal sealed class HostProbe(IProcessTable processes, IHostResources resources, OomPolicyCheck oom, ScriptStaleCheck stale)
{
    internal async ValueTask<Result<HostReading>> Read(CancellationToken cancellationToken)
    {
        var snapshot = await processes.Snapshot(cancellationToken).ConfigureAwait(false);
        var host = await resources.Read(cancellationToken).ConfigureAwait(false);
        var locks = await resources.HeavyLocks(cancellationToken).ConfigureAwait(false);
        if (snapshot.IsError || host.IsError || locks.IsError)
        {
            return snapshot.IsError ? snapshot.Error : host.IsError ? host.Error : locks.Error;
        }

        return new HostReading(snapshot.Value, host.Value, locks.Value, await oom.Warning(cancellationToken).ConfigureAwait(false), await stale.Warning(cancellationToken).ConfigureAwait(false));
    }
}
