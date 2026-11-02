using Aw.Context.Core.Map;

namespace Aw.Context.Core.Concepts;

/// <summary>The glossary read: which map and whose repo.</summary>
/// <param name="Read">Which map: the current version, the draft or a retained version.</param>
/// <param name="Repo">A human caller's repo when the profile has more than one, else null.</param>
public sealed record ConceptGlossaryQuery(MapRead Read, string? Repo = null);
