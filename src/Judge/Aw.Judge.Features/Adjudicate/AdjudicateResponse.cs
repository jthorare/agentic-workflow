namespace Aw.Judge.Features.Adjudicate;

/// <summary>The Adjudicate response.</summary>
/// <param name="Labeled">How many items got an adjudicator label (a 2-of-3 in-enum majority).</param>
/// <param name="Split">How many had no majority.</param>
/// <param name="Failed">How many could not be asked (unparseable input, or every run errored).</param>
public sealed record AdjudicateResponse(int Labeled, int Split, int Failed);
