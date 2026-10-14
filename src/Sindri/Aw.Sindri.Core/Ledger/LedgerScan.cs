namespace Aw.Sindri.Core.Ledger;

/// <summary>One filtered read of the ledger.</summary>
/// <param name="Events">The events the filter kept, oldest first.</param>
/// <param name="ScannedTo">The last sequence the read looked at, kept or not: the caller's next cursor.</param>
public sealed record LedgerScan(IReadOnlyList<LedgerEvent> Events, long ScannedTo);
