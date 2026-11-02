namespace Aw.Context.Core.ConceptScan;

/// <summary>A product concept the first model pass proposed, with the map searches that should show whether it is real.</summary>
/// <param name="Name">The proposed name.</param>
/// <param name="Queries">The words to search the draft map for.</param>
public sealed record ConceptCandidate(string Name, IReadOnlyList<string> Queries);
