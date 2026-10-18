using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Health;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Files;

/// <summary>The host ports on an OS aw-meta can't read the process table and memory of: every call is AW-META-010 naming the OS, never a made-up figure.</summary>
/// <param name="os">The OS's name.</param>
internal sealed class UnsupportedHost(string os) : IProcessTable, IHostResources, IProcessLiveness, IProcessLocations
{
    public ValueTask<Result<HostSnapshot>> Snapshot(CancellationToken cancellationToken) => Unavailable<HostSnapshot>();

    public ValueTask<Result<Unit>> Terminate(int pid, CancellationToken cancellationToken) => Unavailable<Unit>();

    public ValueTask<Result<HostResources>> Read(CancellationToken cancellationToken) => Unavailable<HostResources>();

    public ValueTask<Result<IReadOnlyList<HeavyLock>>> HeavyLocks(CancellationToken cancellationToken) => Unavailable<IReadOnlyList<HeavyLock>>();

    public ValueTask<Result<bool>> IsRunning(int pid, DateTimeOffset recordedAt, CancellationToken cancellationToken) => Unavailable<bool>();

    public ValueTask<Result<IReadOnlyList<ProcessLocation>>> Locations(CancellationToken cancellationToken) => Unavailable<IReadOnlyList<ProcessLocation>>();

    public async ValueTask<IReadOnlyList<Result<bool>>> Running(IReadOnlyList<ProcessClaim> claims, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(claims);
        var answer = await Unavailable<bool>().ConfigureAwait(false);
        return [.. claims.Select(_ => answer)];
    }

    private ValueTask<Result<T>> Unavailable<T>() =>
        ValueTask.FromResult(Results.Failure<T>(MetaErrors.HostUnreadable.With($"unavailable on {os}: aw-meta reads the process table and memory on Linux and macOS only")));
}
