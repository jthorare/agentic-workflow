namespace Aw.Sindri.Core.Lease;

/// <summary>
/// The stored lease as read: the last holder, its epoch, when it was acquired and when it expires. A released lease
/// keeps its last holder and epoch, marked <see cref="Released"/>.
/// </summary>
/// <param name="Holder">The last (or current) holder.</param>
/// <param name="Epoch">The epoch of the last acquisition.</param>
/// <param name="AcquiredAt">When it was acquired.</param>
/// <param name="ExpiresAt">When it lapses (or lapsed, or was released).</param>
/// <param name="Released">Whether the holder released it.</param>
public sealed record LeaseRow(LeaseHolder Holder, long Epoch, DateTimeOffset AcquiredAt, DateTimeOffset ExpiresAt, bool Released)
{
    /// <summary>Whether the lease is still in force at <paramref name="now"/>.</summary>
    /// <param name="now">The current time.</param>
    /// <returns>True while unexpired.</returns>
    public bool IsLive(DateTimeOffset now) => !Released && ExpiresAt > now;
}
