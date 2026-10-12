namespace Aw.Sindri.Core.Lease;

/// <summary>A held lease: the holder, its fencing epoch and when it expires unless renewed.</summary>
/// <param name="Holder">The holder.</param>
/// <param name="Epoch">The fencing epoch; it only ever goes up, by one per acquisition.</param>
/// <param name="ExpiresAt">When the lease lapses without a renewal.</param>
public sealed record LeaseGrant(LeaseHolder Holder, long Epoch, DateTimeOffset ExpiresAt);
