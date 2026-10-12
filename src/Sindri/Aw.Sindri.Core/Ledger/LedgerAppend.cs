namespace Aw.Sindri.Core.Ledger;

/// <summary>The outcome of an append: the event's position, and whether it was already recorded (a replay).</summary>
/// <param name="Sequence">The position of the (new or earlier) event.</param>
/// <param name="AlreadyRecorded">True when an event with the same idempotency key was there already; nothing was written.</param>
public sealed record LedgerAppend(long Sequence, bool AlreadyRecorded);
