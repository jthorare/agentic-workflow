namespace Aw.Sindri.Core.Events;

/// <summary>A hold ended with no promote ('context.map.hold.dropped', code index spec 6.4). Not public.</summary>
/// <param name="Version">The held draft's number.</param>
/// <param name="Reason">Gone (a rebuild replaced the draft, or a person cleared it) or AlreadyCurrent (the defensive guard).</param>
public sealed record ContextMapHoldDropped(int Version, string Reason);
