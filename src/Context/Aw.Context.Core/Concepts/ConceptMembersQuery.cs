using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;

namespace Aw.Context.Core.Concepts;

/// <summary>The members read: one concept's features, records and decisions.</summary>
/// <param name="Concept">The concept's key ('concept:scheduling'), or 'concept:other' for the members no concept takes.</param>
/// <param name="Read">Which map: the current version, the draft or a retained version.</param>
/// <param name="Page">The cursor and the limit.</param>
/// <param name="Repo">A human caller's repo when the profile has more than one, else null.</param>
public sealed record ConceptMembersQuery(NodeKey Concept, MapRead Read, ConceptPage Page, string? Repo = null);
