using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Ledger;

/// <summary>What a <see cref="ILedgerReader.Scan"/> keeps (API spec §5.2, §2.2 ReadLedger).</summary>
/// <param name="Types">The event types to keep, or null for every type.</param>
/// <param name="Item">The work item to keep, or null for every event.</param>
/// <param name="ScanLimit">The most rows one call looks at past its cursor, so a sparse filter never walks the whole ledger.</param>
public sealed record LedgerFilter(TypeFilter? Types, ItemId? Item, int ScanLimit = 5_000)
{
    /// <summary>Every event, with the default scan limit.</summary>
    public static LedgerFilter All { get; } = new(Types: null, Item: null);
}
