using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Health;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Files;

/// <summary>
/// IHostResources over /proc/meminfo, the state disk and the heavy-job lock directories. A lock slot is a directory
/// (the lock path, then .2, .3 ...) whose owner file starts with the holder's pid; a slot with no readable owner is
/// listed with no pid.
/// </summary>
/// <param name="stateDirectory">A directory on the disk whose free space is reported.</param>
/// <param name="heavyLock">The heavy-job lock path (slot 1); the other slots are its siblings.</param>
internal sealed class ProcHostResources(string stateDirectory, string heavyLock) : IHostResources
{
    public async ValueTask<Result<HostResources>> Read(CancellationToken cancellationToken)
    {
        try
        {
            var meminfo = ProcFormat.MemInfo(await File.ReadAllTextAsync("/proc/meminfo", cancellationToken).ConfigureAwait(false));
            return new HostResources(meminfo.Total, meminfo.Available, meminfo.SwapUsed, DiskSpace.Free(stateDirectory), meminfo.SwapTotal);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException)
        {
            return MetaErrors.HostUnreadable.With(exception.Message);
        }
    }

    public ValueTask<Result<IReadOnlyList<HeavyLock>>> HeavyLocks(CancellationToken cancellationToken) =>
        HeavyLockDirectory.List(heavyLock, cancellationToken);
}
