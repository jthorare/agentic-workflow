using Aw.Context.Core.Engine;

namespace Aw.Context.Core.Linking;

/// <summary>A pinned concept's id and its owner's aliases, as the record linker reads them (concept pins spec §3.4).</summary>
/// <param name="Id">The pin id, resolved (PinIds): its node is concept:&lt;id&gt;.</param>
/// <param name="Aliases">The approved aliases, as written in the profile.</param>
public sealed record PinAliases(ConceptId Id, IReadOnlyList<string> Aliases);
