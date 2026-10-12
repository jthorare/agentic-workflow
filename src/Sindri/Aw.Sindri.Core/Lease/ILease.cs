using Aw.Kernel;

namespace Aw.Sindri.Core.Lease;

/// <summary>The host lease (port; factory addendum §17.20, §17.22 D2): one holder per host, fenced by a rising epoch.</summary>
public interface ILease
{
    /// <summary>Takes the lease for <paramref name="candidate"/> when it is free, expired, or held by a provably dead process.</summary>
    /// <param name="candidate">Who asks.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The grant, or SND-LEASE-001 naming the live holder.</returns>
    public ValueTask<Result<LeaseGrant>> Acquire(LeaseHolder candidate, CancellationToken cancellationToken);

    /// <summary>Extends a grant by the TTL.</summary>
    /// <param name="grant">The grant.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The extended grant, or SND-LEASE-002 when it was lost.</returns>
    public ValueTask<Result<LeaseGrant>> Renew(LeaseGrant grant, CancellationToken cancellationToken);

    /// <summary>Gives the lease up; the epoch stays, so the next holder gets epoch + 1.</summary>
    /// <param name="grant">The grant.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success, or SND-LEASE-002 when the grant isn't current.</returns>
    public ValueTask<Result<Unit>> Release(LeaseGrant grant, CancellationToken cancellationToken);

    /// <summary>The stored lease, or null when it was never held.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The row.</returns>
    public ValueTask<Result<LeaseRow?>> Read(CancellationToken cancellationToken);
}
