using Aw.Kernel;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Tests.Server;

/// <summary>Approvals that count reads (one per scheduler loop) and can throw on the first few, like an unreadable file.</summary>
internal sealed class CountingApprovals(IApprovedProfile inner, int throwFirst = 0, Func<Exception>? failure = null) : IApprovedProfile
{
    private int _reads;

    public int Reads => Volatile.Read(ref _reads);

    public ValueTask<Result<LoadedProfile?>> Latest(CancellationToken cancellationToken) =>
        Interlocked.Increment(ref _reads) <= throwFirst
            ? throw (failure?.Invoke() ?? new IOException("Permission denied"))
            : inner.Latest(cancellationToken);
}
