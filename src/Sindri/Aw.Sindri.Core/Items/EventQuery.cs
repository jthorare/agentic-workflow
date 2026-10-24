using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Core.Items;

/// <summary>What <see cref="CallerEventView.Read"/> reads: a cursor, a page size, the ledger filter, whether non-public types come too, and an extra row test.</summary>
/// <param name="After">The last sequence the consumer saw (0 for the start).</param>
/// <param name="Max">The most events to return, at least 1.</param>
/// <param name="Filter">The types and item the ledger keeps.</param>
/// <param name="IncludeNonPublic">True for the ledger read: types outside the catalog's public ones come too, and every payload is whole.</param>
/// <param name="Keep">A further test on each kept row, or null.</param>
public sealed record EventQuery(long After, int Max, LedgerFilter Filter, bool IncludeNonPublic, Func<LedgerEvent, bool>? Keep = null);
