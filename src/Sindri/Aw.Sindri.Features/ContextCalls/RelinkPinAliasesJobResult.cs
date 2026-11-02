using Aw.Context.Core.Linking;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>What a relink job stored as its result: the pair the tick compares (the aliases hash and the map version) and the counts the pinned card reads.</summary>
/// <param name="AliasesHash">The hash of the pins' aliases this run linked for.</param>
/// <param name="MapVersion">The map version it linked against.</param>
/// <param name="Records">Stored records scanned.</param>
/// <param name="Changed">Records whose alias links changed.</param>
/// <param name="ElapsedMs">How long the run took, in milliseconds.</param>
/// <param name="Pins">Each pin's linked-record count before and after.</param>
public sealed record RelinkPinAliasesJobResult(string AliasesHash, int MapVersion, int Records, int Changed, long ElapsedMs, IReadOnlyList<PinLinkCount> Pins);
