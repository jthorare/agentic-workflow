namespace Aw.Context.Core.ConceptScan;

/// <summary>What one map search found, as lines the second model pass reads.</summary>
/// <param name="Query">The words searched.</param>
/// <param name="Lines">One line per hit: its key and label.</param>
public sealed record ConceptSearchResult(string Query, IReadOnlyList<string> Lines);
