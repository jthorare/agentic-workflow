using Aw.Kernel;
using Aw.Sindri.Core.Lease;

namespace Aw.Sindri.Tests.Server;

/// <summary>
/// A lease whose renewals answer with <see cref="RenewError"/> when it is set, and counts them. <see cref="BeforeRenew"/>
/// runs first, so a test can put a stop in the middle of a renewal; <see cref="BeforeRelease"/> runs before a release.
/// </summary>
internal sealed class ScriptedLease(ILease inner) : ILease
{
    private int _renewals;

    public Error? RenewError { get; set; }

    public int Renewals => Volatile.Read(ref _renewals);

    public Func<LeaseGrant, Task>? BeforeRenew { get; set; }

    public Action? BeforeRelease { get; set; }

    public ValueTask<Result<LeaseGrant>> Acquire(LeaseHolder candidate, CancellationToken cancellationToken) => inner.Acquire(candidate, cancellationToken);

    public async ValueTask<Result<LeaseGrant>> Renew(LeaseGrant grant, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _renewals);
        if (BeforeRenew is { } before)
        {
            await before(grant).ConfigureAwait(false);
        }

        return RenewError is { } error ? error : await inner.Renew(grant, cancellationToken).ConfigureAwait(false);
    }

    public ValueTask<Result<Unit>> Release(LeaseGrant grant, CancellationToken cancellationToken)
    {
        BeforeRelease?.Invoke();
        return inner.Release(grant, cancellationToken);
    }

    public ValueTask<Result<LeaseRow?>> Read(CancellationToken cancellationToken) => inner.Read(cancellationToken);
}
