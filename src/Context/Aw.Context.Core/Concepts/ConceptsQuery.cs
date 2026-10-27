using Aw.Context.Core.Map;

namespace Aw.Context.Core.Concepts;

/// <summary>The concepts read: which map, whose repo, a filter and the page.</summary>
/// <param name="Read">Which map: the current version, the draft or a retained version.</param>
/// <param name="Page">The cursor and the limit.</param>
/// <param name="Repo">A human caller's repo when the profile has more than one, else null (a session is always its own repo).</param>
/// <param name="Q">Keeps the concepts whose name, summary or a member's label contains this text (case-insensitive), or null for all.</param>
public sealed record ConceptsQuery(MapRead Read, ConceptPage Page, string? Repo = null, string? Q = null);
