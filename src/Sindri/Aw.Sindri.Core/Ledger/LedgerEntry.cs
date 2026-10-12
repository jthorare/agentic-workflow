using Aw.Kernel.Events;

namespace Aw.Sindri.Core.Ledger;

/// <summary>
/// An event to record: its envelope (payload as JSON text), the idempotency key that makes a replay a no-op
/// (factory addendum §17.20: kind, item, attempt, sha, or what identifies the fact) and the work item it concerns, if any.
/// </summary>
/// <param name="Envelope">The §17.21 envelope; the payload is the event's JSON.</param>
/// <param name="IdempotencyKey">The key; a second entry with the same key is not recorded again.</param>
/// <param name="Item">The work item, when the event concerns one.</param>
public sealed record LedgerEntry(EventEnvelope<string> Envelope, string IdempotencyKey, string? Item);
