using Aw.Kernel.Events;

namespace Aw.Sindri.Core.Ledger;

/// <summary>A recorded event: its position in the ledger, the epoch it was written under, and the entry.</summary>
/// <param name="Sequence">The ledger position, rising by one per event; consumers keep it as their cursor.</param>
/// <param name="Epoch">The lease epoch of the writer.</param>
/// <param name="Envelope">The envelope.</param>
/// <param name="IdempotencyKey">The idempotency key.</param>
/// <param name="Item">The work item, if any.</param>
public sealed record LedgerEvent(long Sequence, long Epoch, EventEnvelope<string> Envelope, string IdempotencyKey, string? Item);
