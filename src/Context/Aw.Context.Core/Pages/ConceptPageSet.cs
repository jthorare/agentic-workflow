namespace Aw.Context.Core.Pages;

/// <summary>The pages that express one concept (<see cref="PageReader.ByConcept"/>).</summary>
/// <param name="Count">How many pages, owner or suggested.</param>
/// <param name="Suggested">The map keys of the suggested ones, in the Pages section's order, at most <see cref="PageReader.SuggestedCap"/>.</param>
public sealed record ConceptPageSet(int Count, IReadOnlyList<string> Suggested);
