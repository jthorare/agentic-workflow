namespace Aw.Judge.Features.AdjudicateSortItems;

/// <summary>The AdjudicateSortItems response.</summary>
/// <param name="Labeled">How many items got an adjudicator label.</param>
/// <param name="Split">How many had no majority.</param>
/// <param name="Failed">How many could not be asked.</param>
public sealed record AdjudicateSortItemsResponse(int Labeled, int Split, int Failed);
