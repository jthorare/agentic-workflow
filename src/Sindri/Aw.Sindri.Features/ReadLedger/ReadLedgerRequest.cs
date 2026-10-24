using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.ReadLedger;

/// <summary>Public events after a cursor, with the next cursor (GET /sindri/ledger; API spec §5.3).</summary>
/// <param name="After">The last sequence the consumer saw (0 for the start).</param>
/// <param name="Item">Only this item's events, or null for every event.</param>
/// <param name="Type">A type glob list such as 'item.*,process.started', or null for every public type.</param>
/// <param name="Max">The most events to return, 1 to 10,000.</param>
public sealed record ReadLedgerRequest(long After, ItemRef? Item = null, string? Type = null, int Max = 100) : IQuery<LedgerPage>;
