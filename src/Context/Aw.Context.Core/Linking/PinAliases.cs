using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;

namespace Aw.Context.Core.Linking;

/// <summary>A pinned concept's id and its owner's aliases, as the record linker reads them (concept pins spec §3.4).</summary>
/// <param name="Id">The pin id, resolved (PinIds): its node is concept:&lt;id&gt;.</param>
/// <param name="Aliases">The approved aliases, as written in the profile.</param>
public sealed record PinAliases(ConceptId Id, IReadOnlyList<string> Aliases)
{
    /// <summary>The pins that carry aliases, with the ids the build gives them (so concept:&lt;id&gt; is the node the build made).</summary>
    /// <param name="pins">The pins of the mapped repos, in the build's order.</param>
    /// <returns>One entry per distinct pin with at least one alias.</returns>
    public static IReadOnlyList<PinAliases> Of(IEnumerable<ConceptPin> pins)
    {
        ArgumentNullException.ThrowIfNull(pins);
        return [.. PinIds.Resolve(pins).Where(r => r.Pin.Aliases.Count > 0 && ConceptId.TryFrom(r.Id, out _)).Select(r => new PinAliases(ConceptId.From(r.Id), r.Pin.Aliases))];
    }
}
