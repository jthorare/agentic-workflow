using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Core.Setup;

/// <summary>One change <see cref="IProfileIndexWriter"/> makes to the top-level 'index.concepts'.</summary>
/// <param name="Id">The entry's id.</param>
/// <param name="Entry">The whole entry to set (its 'id' is <paramref name="Id"/>), or null to remove the entry.</param>
/// <param name="MatchName">The name of an entry with no id that this edit replaces or removes when no entry has <paramref name="Id"/>; matched exactly. Null matches by id only.</param>
public sealed record ConceptEdit(ConceptPinId Id, ConceptSettings? Entry, string? MatchName = null) : IndexEdit;
