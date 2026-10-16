namespace Aw.Sindri.Core.NeedsYou;

/// <summary>A row of the 'needs_you' view as stored: the wire row and the ledger sequence that last changed it.</summary>
/// <param name="Row">The row.</param>
/// <param name="Sequence">The ledger sequence that last changed it.</param>
public sealed record NeedsYouRowRecord(NeedsYouRow Row, long Sequence);
