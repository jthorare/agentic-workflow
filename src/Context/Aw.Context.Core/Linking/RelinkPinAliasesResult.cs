namespace Aw.Context.Core.Linking;

/// <summary>What one relink did (concept pins spec §3.4, §3.8): stored with the job, so the pinned card can say how many records a pin gained.</summary>
/// <param name="AliasesHash">The <see cref="PinAliasSet.Hash"/> of the pins this run linked for.</param>
/// <param name="MapVersion">The map version it linked against.</param>
/// <param name="Records">Stored records scanned.</param>
/// <param name="Changed">Records whose alias links changed.</param>
/// <param name="ElapsedMs">How long the run took, in milliseconds.</param>
/// <param name="Pins">Each pin's linked-record count before and after.</param>
public sealed record RelinkPinAliasesResult(string AliasesHash, int MapVersion, int Records, int Changed, long ElapsedMs, IReadOnlyList<PinLinkCount> Pins);
