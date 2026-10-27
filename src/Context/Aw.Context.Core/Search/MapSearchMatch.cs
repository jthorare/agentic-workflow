namespace Aw.Context.Core.Search;

/// <summary>One document the index matched.</summary>
/// <param name="Doc">The document's id.</param>
/// <param name="Score">Higher is better.</param>
public sealed record MapSearchMatch(int Doc, double Score);
