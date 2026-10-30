using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Core.Setup;

/// <summary>One change <see cref="IProfileConceptWriter"/> makes to 'index.concepts'.</summary>
/// <param name="Id">The entry's id.</param>
/// <param name="Entry">The whole entry to set (its 'id' is <paramref name="Id"/>), or null to remove the entry.</param>
public sealed record ConceptEdit(ConceptPinId Id, ConceptSettings? Entry);
