namespace Aw.Sindri.Core.Packets;

/// <summary>What 'aw-packets freeze' did.</summary>
/// <param name="Frozen">True when the packet passed its checks and its hash is recorded (now or before); false when a check failed and nothing was written.</param>
/// <param name="Hash">The canonical hash ('sha256:…'); null when a check failed.</param>
/// <param name="By">Who froze it, as the stored record says: the first freezer's when the hash was frozen before.</param>
/// <param name="AlreadyFrozen">True when the same hash was frozen before and nothing was written.</param>
/// <param name="AlreadyFrozenBy">Set when the hash was frozen first by someone other than the caller asked for: the call's freezer was not recorded.</param>
/// <param name="Failures">The failed checks; empty when the packet froze.</param>
public sealed record PacketFreeze(bool Frozen, string? Hash, PacketFreezer By, bool AlreadyFrozen, PacketFreezer? AlreadyFrozenBy, IReadOnlyList<PacketFailure> Failures);
