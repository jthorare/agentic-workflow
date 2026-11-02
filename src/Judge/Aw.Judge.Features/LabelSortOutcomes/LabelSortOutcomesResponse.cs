namespace Aw.Judge.Features.LabelSortOutcomes;

/// <summary>The LabelSortOutcomes response.</summary>
/// <param name="Labeled">How many sort items got an outcome label.</param>
/// <param name="NoSignal">How many had no usable next prompt.</param>
public sealed record LabelSortOutcomesResponse(int Labeled, int NoSignal);
